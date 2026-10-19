using TonTuyau.Application.Store;

namespace TonTuyau.Application.Sync;

/// <summary>Everything <c>tontuyau status</c> shows.</summary>
public sealed record SyncStatus(
    SyncLockInfo? Lock,
    IReadOnlyList<SyncRun> RecentRuns,
    int PendingArt,
    IReadOnlyList<IndexStatus> Indices);
