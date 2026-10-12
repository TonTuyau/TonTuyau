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

    /// <returns>The latest runs, most recent first, without their notes and rejected samples.</returns>
    Task<IReadOnlyList<SyncRun>> GetRecentRunsAsync(int count, CancellationToken cancellationToken);
}
