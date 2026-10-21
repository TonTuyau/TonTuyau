using System.Globalization;
using System.Text;
using TonTuyau.Application.Browse;
using TonTuyau.Application.Configuration;
using TonTuyau.Application.Library;
using TonTuyau.Application.Paging;
using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;
using TonTuyau.Application.Validation;

namespace TonTuyau.Application.Dashboard;

/// <summary>
/// The admin dashboard: the library's totals, its play time, its channels (a page at a time), and the import history
/// of a period. Elasticsearch counts (<see cref="IDashboardRepository"/>); this adds them up, divides and picks.
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
            .GetLibraryStatsAsync([], cancellationToken)
            .ConfigureAwait(false);
        SyncRun? lastSuccess = await store
            .GetLatestSuccessfulRunAsync(cancellationToken)
            .ConfigureAwait(false);
        return new DashboardView(
            lastSuccess?.FinishedAt,
            Totals(channels.Count, stats),
            PlayTime(stats));
    }

    /// <summary>
    /// A page of the channels with their counts (alpha.3): those whose name has the words (accents and case
    /// ignored), with videos gone from YouTube or not, sorted by a column. A library has a few hundred channels: all
    /// are counted, then filtered, sorted and cut here; the cursor is where the page ends.
    /// </summary>
    /// <exception cref="ValidationException">The request isn't valid.</exception>
    public async Task<ResultPage<ChannelTotalsView>> ListChannelsAsync(
        DashboardChannelsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidationErrors errors = new();
        RequestRules.Text(errors, "q", request.Q, required: false, ApiValues.MaxChannelFilterLength);
        RequestRules.OneOf(errors, "sort", request.Sort, ApiValues.DashboardChannelSorts);
        RequestRules.OneOf(errors, "order", request.Order, ApiValues.SortDirections);
        RequestRules.Size(errors, request.Size, ApiValues.MaxPageSize);
        errors.ThrowIfAny(RequestRules.Subject);
        string? words = string.IsNullOrWhiteSpace(request.Q) ? null : Fold(request.Q.Trim());
        DashboardChannelSort sort = request.Sort is null
            ? DashboardChannelSort.Total
            : ApiValues.DashboardChannelSorts[request.Sort];
        // Names A to Z first, numbers biggest first.
        bool ascending = request.Order is null
            ? sort == DashboardChannelSort.Name
            : ApiValues.SortDirections[request.Order] == SortDirection.Ascending;
        bool onlyGone = request.Gone ?? false;
        string fingerprint = Fingerprints.Of(
            "dashboard-channels",
            words,
            sort.ToString(),
            ascending ? "asc" : "desc",
            onlyGone ? "gone" : null);
        int offset = OffsetOf(CursorCodec.Decode(request.Cursor, fingerprint));
        int size = request.Size ?? ApiValues.DefaultPageSize;

        IReadOnlyList<ChannelListItem> channels = await store
            .GetChannelsAsync(cancellationToken)
            .ConfigureAwait(false);
        LibraryStats stats = await store
            .GetLibraryStatsAsync([.. channels.Select(c => c.Id)], cancellationToken)
            .ConfigureAwait(false);
        ChannelTotalsView[] matching = Sorted(
            ChannelTotals(channels, stats)
                .Where(c => words is null || Fold(c.Name).Contains(words, StringComparison.Ordinal))
                .Where(c => !onlyGone || c.Gone > 0),
            sort,
            ascending);
        ChannelTotalsView[] shown = [.. matching.Skip(offset).Take(size)];
        int next = offset + shown.Length;
        return new ResultPage<ChannelTotalsView>(
            shown,
            next < matching.Length
                ? CursorCodec.Encode(fingerprint, new SortPosition([new IntegerSortValue(next)]))
                : null,
            matching.Length);
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

    private static IEnumerable<ChannelTotalsView> ChannelTotals(
        IReadOnlyList<ChannelListItem> channels,
        LibraryStats stats) =>
        channels.Select(c =>
        {
            ChannelStats counted = stats.Channels.GetValueOrDefault(c.Id)
                ?? new ChannelStats(TypeCounts.None, 0, 0, 0, 0, 0);
            TypeCounts counts = counted.Counts;
            return new ChannelTotalsView(
                c.Id,
                c.Name,
                c.ThumbArt,
                counts,
                counted.Gone,
                counted.Private,
                counted.Unlisted,
                counted.RetryLater,
                counted.NotChecked,
                counts.Videos + counts.Shorts + counts.Streams);
        });

    /// <summary>By the column, then by name (A to Z, accents and case ignored) and id: the same order every time.</summary>
    private static ChannelTotalsView[] Sorted(
        IEnumerable<ChannelTotalsView> channels,
        DashboardChannelSort sort,
        bool ascending)
    {
        if (sort == DashboardChannelSort.Name)
        {
            IOrderedEnumerable<ChannelTotalsView> byName = ascending
                ? channels.OrderBy(c => Fold(c.Name), StringComparer.Ordinal)
                : channels.OrderByDescending(c => Fold(c.Name), StringComparer.Ordinal);
            return [.. byName.ThenBy(c => c.Id, StringComparer.Ordinal)];
        }

        IOrderedEnumerable<ChannelTotalsView> byCount = ascending
            ? channels.OrderBy(c => CountIn(c, sort))
            : channels.OrderByDescending(c => CountIn(c, sort));
        return [.. byCount
            .ThenBy(c => Fold(c.Name), StringComparer.Ordinal)
            .ThenBy(c => c.Id, StringComparer.Ordinal)];
    }

    private static long CountIn(ChannelTotalsView channel, DashboardChannelSort sort) => sort switch
    {
        DashboardChannelSort.Videos => channel.Counts.Videos,
        DashboardChannelSort.Shorts => channel.Counts.Shorts,
        DashboardChannelSort.Streams => channel.Counts.Streams,
        DashboardChannelSort.Gone => channel.Gone,
        DashboardChannelSort.Private => channel.Private,
        DashboardChannelSort.Unlisted => channel.Unlisted,
        DashboardChannelSort.RetryLater => channel.RetryLater,
        DashboardChannelSort.NotChecked => channel.NotChecked,
        DashboardChannelSort.Total => channel.Total,
        _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, "Not a count."),
    };

    /// <summary>The cursor's one value: how many channels the pages before gave.</summary>
    /// <exception cref="ValidationException">Not a cursor of this list.</exception>
    private static int OffsetOf(PageCursor? cursor)
    {
        if (cursor is null)
        {
            return 0;
        }

        return cursor.After.Values is [IntegerSortValue { Value: > 0 and <= int.MaxValue } offset]
            ? (int)offset.Value
            : throw CursorCodec.Invalid();
    }

    /// <summary>Lower case, accents dropped: "Études" finds "etudes".</summary>
    private static string Fold(string text)
    {
        StringBuilder folded = new(text.Length);
        foreach (char c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                folded.Append(char.ToLowerInvariant(c));
            }
        }

        return folded.ToString();
    }

    private static long CountOf(LibraryStats stats, VideoType type) =>
        stats.Types.FirstOrDefault(s => s.Type == type)?.Count ?? 0;
}
