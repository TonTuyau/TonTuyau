using TonTuyau.Application.Library;
using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;

namespace TonTuyau.Application.Dashboard;

/// <summary>
/// The admin dashboard: the library's totals, its play time, the recent imports and every channel. Elasticsearch counts
/// (<see cref="IDashboardRepository"/>); this adds them up, divides and picks.
/// </summary>
public sealed class DashboardService(IDashboardRepository store)
{
    /// <summary>
    /// How many runs are read to find the imports. The run history only indexes a few fields, not the counts, so the
    /// runs that brought videos are picked here: 500 runs is about a month with the default crons (hourly).
    /// </summary>
    public const int RunsRead = 500;

    /// <summary>The bars of the import history.</summary>
    public const int ImportsShown = 30;

    private static readonly VideoType[] Types = [VideoType.Video, VideoType.Short, VideoType.Stream];

    public async Task<DashboardView> GetAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ChannelListItem> channels = await store
            .GetChannelsAsync(cancellationToken)
            .ConfigureAwait(false);
        LibraryStats stats = await store
            .GetLibraryStatsAsync([.. channels.Select(c => c.Id)], cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<SyncRun> runs = await store
            .GetRecentRunsAsync(RunsRead, cancellationToken)
            .ConfigureAwait(false);
        return new DashboardView(
            runs.FirstOrDefault(r => r.Outcome == SyncOutcome.Succeeded)?.FinishedAt,
            Totals(channels.Count, stats),
            PlayTime(stats),
            Imports(runs),
            ChannelTotals(channels, stats));
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

    /// <summary>
    /// The finished runs that brought new videos. An index rebuild is left out: it writes the whole library again into
    /// new indices, so every video counts as new there.
    /// </summary>
    private static ImportView[] Imports(IReadOnlyList<SyncRun> runs) =>
        runs
            .Where(r => r.FinishedAt is not null && !r.IndexRebuild && r.Counts.Created > 0)
            .Take(ImportsShown)
            .Reverse()
            .Select(r => new ImportView(r.StartedAt, r.Counts.Created, r.Counts.CreatedByType))
            .ToArray();

    private static ChannelTotalsView[] ChannelTotals(IReadOnlyList<ChannelListItem> channels, LibraryStats stats) =>
        channels
            .Select(c =>
            {
                ChannelStats counted = stats.Channels.GetValueOrDefault(c.Id) ?? new ChannelStats(TypeCounts.None, 0);
                TypeCounts counts = counted.Counts;
                return new ChannelTotalsView(
                    c.Id,
                    c.Name,
                    c.ThumbArt,
                    counts,
                    counted.Gone,
                    counts.Videos + counts.Shorts + counts.Streams);
            })
            .OrderByDescending(c => c.Total)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static long CountOf(LibraryStats stats, VideoType type) =>
        stats.Types.FirstOrDefault(s => s.Type == type)?.Count ?? 0;
}
