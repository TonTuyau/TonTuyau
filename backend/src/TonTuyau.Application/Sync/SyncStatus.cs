using TonTuyau.Application.Store;

namespace TonTuyau.Application.Sync;

/// <summary>Everything <c>tontuyau sync status</c> shows.</summary>
public sealed record SyncStatus(
    SyncCheckpoint Checkpoint,
    SyncLockInfo? Lock,
    IReadOnlyList<SyncRun> RecentRuns,
    int PendingArt,
    IReadOnlyList<IndexStatus> Indices);
