using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;

namespace TonTuyau.Application.Dashboard;

/// <summary>
/// What the admin dashboard reads in TonTuyau's Elasticsearch: storage calls only, the numbers are counted by
/// Elasticsearch's aggregations, <see cref="DashboardService"/> shapes them.
/// </summary>
public interface IDashboardRepository
{
    /// <returns>Every channel users see, in no particular order.</returns>
    Task<IReadOnlyList<ChannelListItem>> GetChannelsAsync(CancellationToken cancellationToken);

    /// <returns>The videos users see, counted per type and per channel (only these channels).</returns>
    Task<LibraryStats> GetLibraryStatsAsync(
        IReadOnlyCollection<string> channelIds,
        CancellationToken cancellationToken);

    /// <returns>The latest sync (incremental or full run) that succeeded, or null when none did.</returns>
    Task<SyncRun?> GetLatestSuccessfulRunAsync(CancellationToken cancellationToken);

    /// <param name="since">Null for every run.</param>
    /// <returns>
    /// The syncs (incremental and full runs, not the runs that applied deletions) started from <paramref name="since"/>
    /// on, oldest first, with only what the import history reads: when they started and finished, whether they rebuilt
    /// the indices, their new videos.
    /// </returns>
    Task<IReadOnlyList<SyncRun>> GetRunsSinceAsync(DateTimeOffset? since, CancellationToken cancellationToken);
}
