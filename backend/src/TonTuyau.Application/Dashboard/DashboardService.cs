using TonTuyau.Application.Browse;
using TonTuyau.Application.Configuration;
using TonTuyau.Application.Library;
using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;
using TonTuyau.Application.Validation;

namespace TonTuyau.Application.Dashboard;

/// <summary>
/// The admin dashboard: the library's totals, its play time, every channel, and the import history of a period.
/// Elasticsearch counts (<see cref="IDashboardRepository"/>); this adds them up, divides and picks.
/// </summary>
public sealed class DashboardService(
    IDashboardRepository store,
    IValidator<ImportHistoryRequest> importsValidator,
    TonTuyauOptions options,
    TimeProvider clock)
{
    public const ImportPeriod DefaultImportPeriod = ImportPeriod.Month;

    private static readonly VideoType[] Types = [VideoType.Video, VideoType.Short, VideoType.Stream];

    private readonly TimeZoneInfo _zone = TimeZoneInfo.FindSystemTimeZoneById(options.Sync.TimeZone);

    public async Task<DashboardView> GetAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ChannelListItem> channels = await store
            .GetChannelsAsync(cancellationToken)
            .ConfigureAwait(false);
        LibraryStats stats = await store
            .GetLibraryStatsAsync([.. channels.Select(c => c.Id)], cancellationToken)
            .ConfigureAwait(false);
        SyncRun? lastSuccess = await store
            .GetLatestSuccessfulRunAsync(cancellationToken)
            .ConfigureAwait(false);
        return new DashboardView(
            lastSuccess?.FinishedAt,
            Totals(channels.Count, stats),
            PlayTime(stats),
            ChannelTotals(channels, stats));
    }

    /// <summary>The import history of a period: its own call, so that changing the period reads the runs only.</summary>
    /// <exception cref="ValidationException">An unknown period.</exception>
    public async Task<ImportHistoryView> GetImportsAsync(
        ImportHistoryRequest request,
        CancellationToken cancellationToken)
    {
        importsValidator.Validate(request);
        ImportPeriod period = request.Period is null ? DefaultImportPeriod : ApiValues.ImportPeriods[request.Period];
        DateTimeOffset now = clock.GetUtcNow();
        IReadOnlyList<SyncRun> runs = await store
            .GetRunsSinceAsync(ImportHistory.Since(period, now, _zone), cancellationToken)
            .ConfigureAwait(false);
        return new ImportHistoryView(
            ImportHistory.StepOf(period),
            options.Sync.TimeZone,
            ImportHistory.Bars(period, runs, now, _zone));
    }

    private static LibraryTotalsView Totals(int channels, LibraryStats stats) =>
        new(
            channels,
            CountOf(stats, VideoType.Video),
            CountOf(stats, VideoType.Short),
            CountOf(stats, VideoType.Stream),
            stats.Gone);

    private static PlayTimeView PlayTime(LibraryStats stats)
    {
        TypeStats[] types = Types
            .Select(type => stats.Types.FirstOrDefault(s => s.Type == type) ?? new TypeStats(type, 0, TimeSpan.Zero))
            .ToArray();
        TimeSpan total = types.Aggregate(TimeSpan.Zero, (sum, s) => sum + s.Duration);
        return new PlayTimeView(
            total,
            [
                .. types.Select(s => new TypePlayTimeView(
                    s.Type,
                    s.Count,
                    s.Duration,
                    total > TimeSpan.Zero ? s.Duration / total : 0,
                    s.Count > 0 ? s.Duration / s.Count : TimeSpan.Zero)),
            ]);
    }

    private static ChannelTotalsView[] ChannelTotals(IReadOnlyList<ChannelListItem> channels, LibraryStats stats) =>
        channels
            .Select(c =>
            {
                ChannelStats counted = stats.Channels.GetValueOrDefault(c.Id)
                    ?? new ChannelStats(TypeCounts.None, 0, 0, 0);
                TypeCounts counts = counted.Counts;
                return new ChannelTotalsView(
                    c.Id,
                    c.Name,
                    c.ThumbArt,
                    counts,
                    counted.Gone,
                    counted.RetryLater,
                    counted.NotChecked,
                    counts.Videos + counts.Shorts + counts.Streams);
            })
            .OrderByDescending(c => c.Total)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static long CountOf(LibraryStats stats, VideoType type) =>
        stats.Types.FirstOrDefault(s => s.Type == type)?.Count ?? 0;
}
