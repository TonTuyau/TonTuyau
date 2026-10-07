namespace TonTuyau.Application.Sync;

/// <summary>A held sync lock. Renewed in the background; disposing it releases the lock.</summary>
public interface ISyncLease : IAsyncDisposable
{
    SyncLockInfo Info { get; }

    /// <summary>Cancelled if the lease couldn't be renewed (someone else holds the lock now): the run must stop.</summary>
    CancellationToken Lost { get; }
}

/// <summary>Sync state kept in TonTuyau's Elasticsearch (index <c>&lt;prefix&gt;-sync</c>), shared by every process.</summary>
public interface ISyncStateStore
{
    /// <summary>Creates the sync index if it's missing.</summary>
    Task EnsureAsync(CancellationToken cancellationToken);

    Task<SyncCheckpoint> GetCheckpointAsync(CancellationToken cancellationToken);

    Task SaveCheckpointAsync(SyncCheckpoint checkpoint, CancellationToken cancellationToken);

    /// <summary>
    /// Takes the lock if it's free or its lease has expired (a crashed holder).
    /// </summary>
    /// <returns>The lease, or null when another live process holds the lock.</returns>
    Task<ISyncLease?> TryAcquireLockAsync(SyncMode mode, SyncTrigger trigger, CancellationToken cancellationToken);

    Task<SyncLockInfo?> GetLockAsync(CancellationToken cancellationToken);

    Task SaveRunAsync(SyncRun run, CancellationToken cancellationToken);

    Task<SyncRun?> GetRunAsync(string id, CancellationToken cancellationToken);

    /// <returns>Most recent first.</returns>
    Task<IReadOnlyList<SyncRun>> GetRecentRunsAsync(int count, CancellationToken cancellationToken);

    /// <returns>The most recent full run that finished (succeeded or failed), or null.</returns>
    Task<SyncRun?> GetLatestFullRunAsync(CancellationToken cancellationToken);

    /// <returns>TubeArchivist cache paths of art that failed to copy and must be retried.</returns>
    Task<IReadOnlySet<string>> GetPendingArtAsync(CancellationToken cancellationToken);

    Task SavePendingArtAsync(IReadOnlySet<string> paths, CancellationToken cancellationToken);
}

/// <summary>Lock timing. Production: 10 min lease renewed every minute (SYNC_DESIGN.md); tests use seconds.</summary>
public sealed record SyncLockSettings(TimeSpan Lease, TimeSpan RenewEvery)
{
    public static readonly SyncLockSettings Default = new(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(1));
}
