using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using TonTuyau.Application.Sync;
using TonTuyau.Infrastructure.Accounts;

namespace TonTuyau.Infrastructure.Runs;

/// <summary>
/// The runs' state in PostgreSQL (2.0), shared by every process: the history (<c>runs</c>), the lock
/// (<c>sync_lock</c>), the pictures to copy again. A singleton like the import, a
/// database context per call. The lock is a row with a lease: taken by inserting it, renewed by its owner while the
/// run lives, released by its owner; one whose lease expired (a crashed process) is taken over.
/// </summary>
internal sealed partial class SyncStateStore(
    IServiceScopeFactory scopes,
    SyncLockSettings lockSettings,
    TimeProvider clock,
    ILogger<SyncStateStore> logger) : ISyncStateStore
{
    public async Task<ISyncLease?> TryAcquireLockAsync(
        SyncMode mode,
        SyncTrigger trigger,
        CancellationToken cancellationToken)
    {
        // Two attempts: the holder may release between our failed insert and our read.
        for (int attempt = 0; attempt < 2; attempt++)
        {
            DateTimeOffset now = clock.GetUtcNow();
            SyncLockInfo info = new(Owner(), mode, trigger, now, now + lockSettings.Lease);
            int inserted = await WithContextAsync(db => db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO sync_lock (id, owner, mode, trigger, acquired_at, lease_until)
                VALUES ({SyncLockRow.OnlyId}, {info.Owner}, {mode.ToString()}, {trigger.ToString()},
                    {now}, {info.LeaseUntil})
                ON CONFLICT (id) DO NOTHING
                """,
                cancellationToken))
                .ConfigureAwait(false);
            if (inserted == 1)
            {
                return StartLease(info);
            }

            SyncLockRow? holder = await FindLockAsync(cancellationToken).ConfigureAwait(false);
            if (holder is null)
            {
                continue;
            }

            if (holder.LeaseUntil > now)
            {
                return null;
            }

            // The holder's lease expired (crashed process): take over, unless someone else just did.
            LogTakeover(holder.Owner, holder.LeaseUntil);
            int taken = await WithContextAsync(db => db.SyncLock
                .Where(l => l.Id == SyncLockRow.OnlyId
                    && l.Owner == holder.Owner
                    && l.LeaseUntil == holder.LeaseUntil)
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(l => l.Owner, info.Owner)
                        .SetProperty(l => l.Mode, mode)
                        .SetProperty(l => l.Trigger, trigger)
                        .SetProperty(l => l.AcquiredAt, now)
                        .SetProperty(l => l.LeaseUntil, info.LeaseUntil),
                    cancellationToken))
                .ConfigureAwait(false);
            return taken == 1 ? StartLease(info) : null;
        }

        return null;
    }

    public async Task<SyncLockInfo?> GetLockAsync(CancellationToken cancellationToken) =>
        await FindLockAsync(cancellationToken).ConfigureAwait(false) is { } row
            ? new SyncLockInfo(row.Owner, row.Mode, row.Trigger, row.AcquiredAt, row.LeaseUntil)
            : null;

    public Task SaveRunAsync(SyncRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        RunRow written = RunJson.ToRow(run);
        return WithContextAsync(async db =>
        {
            RunRow? row = await db.Runs
                .FirstOrDefaultAsync(r => r.Id == run.Id, cancellationToken)
                .ConfigureAwait(false);
            if (row is null)
            {
                db.Runs.Add(written);
            }
            else
            {
                db.Entry(row).CurrentValues.SetValues(written);
            }

            return await db
                .SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);
        });
    }

    public async Task<SyncRun?> GetRunAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        string? json = await WithContextAsync(db => db.Runs
            .Where(r => r.Id == id)
            .Select(r => r.Run)
            .FirstOrDefaultAsync(cancellationToken))
            .ConfigureAwait(false);
        return json is null ? null : RunJson.Read(json);
    }

    public async Task<IReadOnlyList<SyncRun>> GetRecentRunsAsync(int count, CancellationToken cancellationToken)
    {
        List<string> runs = await WithContextAsync(db => db.Runs
            .OrderByDescending(r => r.StartedAt)
            .ThenByDescending(r => r.Id)
            .Select(r => r.Run)
            .Take(count)
            .ToListAsync(cancellationToken))
            .ConfigureAwait(false);
        return [.. runs.Select(RunJson.Read)];
    }

    public async Task<IReadOnlySet<string>> GetPendingArtAsync(CancellationToken cancellationToken)
    {
        List<string> paths = await WithContextAsync(db => db.PendingArt
            .Select(p => p.Path)
            .ToListAsync(cancellationToken))
            .ConfigureAwait(false);
        return paths.ToHashSet(StringComparer.Ordinal);
    }

    public Task SavePendingArtAsync(IReadOnlySet<string> paths, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return WithContextAsync(async db =>
        {
            IDbContextTransaction transaction = await db.Database
                .BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                await db.PendingArt
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);
                db.PendingArt.AddRange(paths.Select(p => new PendingArtRow { Path = p }));
                int saved = await db
                    .SaveChangesAsync(cancellationToken)
                    .ConfigureAwait(false);
                await transaction
                    .CommitAsync(cancellationToken)
                    .ConfigureAwait(false);
                return saved;
            }
        });
    }

    private SyncLockSettings LockSettings => lockSettings;

    private TimeProvider Clock => clock;

    private static string Owner() =>
        $"{Environment.MachineName}/{Environment.ProcessId}/{Guid.NewGuid().ToString("N")[..8]}";

    private Task<SyncLockRow?> FindLockAsync(CancellationToken cancellationToken) =>
        WithContextAsync(db => db.SyncLock
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == SyncLockRow.OnlyId, cancellationToken));

    private Lease StartLease(SyncLockInfo info) => new(this, info);

    private async Task<T> WithContextAsync<T>(Func<TonTuyauDbContext, Task<T>> work)
    {
        AsyncServiceScope scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            return await work(scope.ServiceProvider.GetRequiredService<TonTuyauDbContext>()).ConfigureAwait(false);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Taking over the run lock from {Owner}, whose lease expired at {LeaseUntil}.")]
    private partial void LogTakeover(string owner, DateTimeOffset leaseUntil);

    [LoggerMessage(Level = LogLevel.Error, Message = "Run lock lost: another process holds it now. The run stops.")]
    private partial void LogLost();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Couldn't renew the run lock ({Reason}); retrying.")]
    private partial void LogRenewFailed(string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Couldn't release the run lock: it expires with its lease.")]
    private partial void LogReleaseFailed(Exception exception);

    /// <summary>Renews the lock every <see cref="SyncLockSettings.RenewEvery"/> until disposed, then releases it.</summary>
    private sealed class Lease : ISyncLease
    {
        private readonly SyncStateStore _store;
        private readonly CancellationTokenSource _lost = new();
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _renewing;

        public Lease(SyncStateStore store, SyncLockInfo info)
        {
            _store = store;
            Info = info;
            _renewing = Task.Run(RenewLoopAsync);
        }

        public SyncLockInfo Info { get; private set; }

        public CancellationToken Lost => _lost.Token;

        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync().ConfigureAwait(false);
            await _renewing.ConfigureAwait(false);
            try
            {
                // Only our own lock: never delete a lock someone else holds now.
                string owner = Info.Owner;
                await _store.WithContextAsync(db => db.SyncLock
                    .Where(l => l.Id == SyncLockRow.OnlyId && l.Owner == owner)
                    .ExecuteDeleteAsync(CancellationToken.None))
                    .ConfigureAwait(false);
            }
            catch (NpgsqlException ex)
            {
                // PostgreSQL unreachable: the lease simply expires.
                _store.LogReleaseFailed(ex);
            }

            _lost.Dispose();
            _stop.Dispose();
        }

        private async Task RenewLoopAsync()
        {
            SyncLockSettings settings = _store.LockSettings;
            while (!_stop.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(settings.RenewEvery, _store.Clock, _stop.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return; // Disposed: normal end of the run.
                }

                DateTimeOffset now = _store.Clock.GetUtcNow();
                SyncLockInfo renewed = Info with { LeaseUntil = now + settings.Lease };
                try
                {
                    string owner = Info.Owner;
                    int written = await _store.WithContextAsync(db => db.SyncLock
                        .Where(l => l.Id == SyncLockRow.OnlyId && l.Owner == owner)
                        .ExecuteUpdateAsync(s => s.SetProperty(l => l.LeaseUntil, renewed.LeaseUntil), _stop.Token))
                        .ConfigureAwait(false);
                    if (written == 0)
                    {
                        _store.LogLost();
                        await _lost.CancelAsync().ConfigureAwait(false);
                        return;
                    }

                    Info = renewed;
                }
                catch (OperationCanceledException)
                {
                    return; // Disposed while renewing.
                }
                catch (NpgsqlException ex)
                {
                    _store.LogRenewFailed(ex.Message);
                    if (now >= Info.LeaseUntil)
                    {
                        // Our lease is over: another process may legitimately take the lock now.
                        _store.LogLost();
                        await _lost.CancelAsync().ConfigureAwait(false);
                        return;
                    }
                }
            }
        }
    }
}
