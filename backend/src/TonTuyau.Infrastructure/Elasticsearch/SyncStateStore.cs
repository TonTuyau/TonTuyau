using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Microsoft.Extensions.Logging;
using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;

namespace TonTuyau.Infrastructure.Elasticsearch;

/// <summary>
/// Sync state: lock, checkpoint and pending art in <c>&lt;prefix&gt;-sync</c> (read by id), run history in
/// <c>&lt;prefix&gt;-sync-runs</c> (searched). The lock is one document written with optimistic concurrency
/// (create, then <c>if_seq_no</c> + <c>if_primary_term</c>), so two processes can never both believe they hold it.
/// </summary>
internal sealed partial class SyncStateStore(
    ElasticsearchClient client,
    IndexNames names,
    SyncLockSettings lockSettings,
    TimeProvider clock,
    ILogger<SyncStateStore> logger) : ISyncStateStore
{
    private const string LockId = "lock";
    private const string CheckpointId = "checkpoint";
    private const string PendingArtId = "pending_art";

    public async Task EnsureAsync(CancellationToken cancellationToken)
    {
        await client.CreateIndexFromDefinitionAsync(names.Sync, IndexDefinitions.Raw("sync"), cancellationToken)
            .ConfigureAwait(false);
        await client
            .CreateIndexFromDefinitionAsync(names.SyncRuns, IndexDefinitions.Raw("sync-runs"), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<SyncCheckpoint> GetCheckpointAsync(CancellationToken cancellationToken) =>
        (await GetStateAsync<SyncCheckpoint>(CheckpointId, cancellationToken).ConfigureAwait(false)).Source
            ?? SyncCheckpoint.Empty;

    public Task SaveCheckpointAsync(SyncCheckpoint checkpoint, CancellationToken cancellationToken) =>
        PutStateAsync(CheckpointId, checkpoint, cancellationToken);

    public async Task<ISyncLease?> TryAcquireLockAsync(
        SyncMode mode,
        SyncTrigger trigger,
        CancellationToken cancellationToken)
    {
        // Two attempts: the holder may release between our failed create and our read.
        for (int attempt = 0; attempt < 2; attempt++)
        {
            DateTimeOffset now = clock.GetUtcNow();
            SyncLockInfo info = new(Owner(), mode, trigger, now, now + lockSettings.Lease);
            CreateResponse created = (await client
                .CreateAsync(new CreateRequest<SyncLockInfo>(info, names.Sync, LockId), cancellationToken)
                .ConfigureAwait(false))
                .Ensure("Take the sync lock", 409);
            if (created.Status() != 409)
            {
                return StartLease(info, created.SeqNo, created.PrimaryTerm);
            }

            GetResponse<SyncLockInfo> holder = await GetStateAsync<SyncLockInfo>(LockId, cancellationToken)
                .ConfigureAwait(false);
            if (!holder.Found || holder.Source is null)
            {
                continue;
            }

            if (holder.Source.LeaseUntil > now)
            {
                return null;
            }

            // The holder's lease expired (crashed process): take over, unless someone else just did.
            LogTakeover(holder.Source.Owner, holder.Source.LeaseUntil);
            IndexRequest<SyncLockInfo> takeover = new(info, names.Sync, LockId)
            {
                IfSeqNo = holder.SeqNo,
                IfPrimaryTerm = holder.PrimaryTerm,
            };
            IndexResponse taken = (await client.IndexAsync(takeover, cancellationToken).ConfigureAwait(false))
                .Ensure("Take over the sync lock", 409);
            return taken.Status() == 409 ? null : StartLease(info, taken.SeqNo, taken.PrimaryTerm);
        }

        return null;
    }

    public async Task<SyncLockInfo?> GetLockAsync(CancellationToken cancellationToken) =>
        (await GetStateAsync<SyncLockInfo>(LockId, cancellationToken).ConfigureAwait(false)).Source;

    public async Task SaveRunAsync(SyncRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        IndexRequest<SyncRun> request = new(run, names.SyncRuns, run.Id) { Refresh = Refresh.True };
        (await client.IndexAsync(request, cancellationToken).ConfigureAwait(false)).Ensure($"Save run {run.Id}");
    }

    public async Task<SyncRun?> GetRunAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        GetResponse<SyncRun> response = (await client
            .GetAsync<SyncRun>(new GetRequest(names.SyncRuns, id), cancellationToken)
            .ConfigureAwait(false))
            .Ensure($"Get run {id}", 404);
        return response.Found ? response.Source : null;
    }

    public Task<IReadOnlyList<SyncRun>> GetRecentRunsAsync(int count, CancellationToken cancellationToken) =>
        SearchRunsAsync(new MatchAllQuery(), count, cancellationToken);

    public async Task<SyncRun?> GetLatestFullRunAsync(CancellationToken cancellationToken)
    {
        BoolQuery finishedFullRuns = new()
        {
            Filter =
            [
                new TermQuery { Field = Infer.Field<SyncRun>(r => r.Mode), Value = "full" },
                new TermsQuery
                {
                    Field = Infer.Field<SyncRun>(r => r.Outcome),
                    Terms = new TermsQueryField([FieldValue.String("succeeded"), FieldValue.String("failed")]),
                },
            ],
        };
        IReadOnlyList<SyncRun> runs = await SearchRunsAsync(finishedFullRuns, 1, cancellationToken).ConfigureAwait(
            false);
        return runs.Count == 0 ? null : runs[0];
    }

    public async Task<IReadOnlySet<string>> GetPendingArtAsync(CancellationToken cancellationToken)
    {
        PendingArtDocument? pending = (await GetStateAsync<PendingArtDocument>(PendingArtId, cancellationToken)
            .ConfigureAwait(false))
            .Source;
        return new HashSet<string>(pending?.Paths ?? [], StringComparer.Ordinal);
    }

    public Task SavePendingArtAsync(IReadOnlySet<string> paths, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paths);
        string[] sorted = paths
            .Order(StringComparer.Ordinal)
            .ToArray();
        return PutStateAsync(PendingArtId, new PendingArtDocument(sorted), cancellationToken);
    }

    private static string Owner() =>
        $"{Environment.MachineName}/{Environment.ProcessId}/{Guid.NewGuid().ToString("N")[..8]}";

    private Lease StartLease(SyncLockInfo info, long? seqNo, long? primaryTerm) =>
        new(
            new LeaseContext(client, names.Sync, lockSettings, clock, LogLost, LogRenewFailed, LogReleaseFailed),
            info,
            seqNo ?? throw new SearchStoreException("Elasticsearch didn't return the lock's _seq_no."),
            primaryTerm ?? throw new SearchStoreException("Elasticsearch didn't return the lock's _primary_term."));

    private async Task<IReadOnlyList<SyncRun>> SearchRunsAsync(
        Query query,
        int size,
        CancellationToken cancellationToken)
    {
        SearchRequest<SyncRun> request = new(names.SyncRuns)
        {
            Size = size,
            Query = query,
            Sort = [new SortOptions {
                Field = new FieldSort { Field = Infer.Field<SyncRun>(r => r.StartedAt), Order = SortOrder.Desc } }],
        };
        SearchResponse<SyncRun> response = (await client.SearchAsync<SyncRun>(request, cancellationToken)
            .ConfigureAwait(false))
            .Ensure($"Search {names.SyncRuns}");
        return response.Hits
            .Select(h => h.Source)
            .OfType<SyncRun>()
            .ToArray();
    }

    private async Task<GetResponse<T>> GetStateAsync<T>(string id, CancellationToken cancellationToken) =>
        (await client
            .GetAsync<T>(new GetRequest(names.Sync, id), cancellationToken)
            .ConfigureAwait(false))
            .Ensure($"Get {names.Sync}/{id}", 404);

    private async Task PutStateAsync<T>(string id, T document, CancellationToken cancellationToken) =>
        (await client
            .IndexAsync(new IndexRequest<T>(document, names.Sync, id), cancellationToken)
            .ConfigureAwait(false))
            .Ensure($"Save {names.Sync}/{id}");

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Taking over the sync lock from {Owner}, whose lease expired at {LeaseUntil}.")]
    private partial void LogTakeover(string owner, DateTimeOffset leaseUntil);

    [LoggerMessage(Level = LogLevel.Error, Message = "Sync lock lost: another process holds it now. The run stops.")]
    private partial void LogLost();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Couldn't renew the sync lock ({Reason}); retrying.")]
    private partial void LogRenewFailed(string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Couldn't release the sync lock: it expires with its lease.")]
    private partial void LogReleaseFailed(Exception exception);

    private sealed record PendingArtDocument(IReadOnlyList<string> Paths);

    private sealed record LeaseContext(
        ElasticsearchClient Client,
        string Index,
        SyncLockSettings Settings,
        TimeProvider Clock,
        Action OnLost,
        Action<string> OnRenewFailed,
        Action<Exception> OnReleaseFailed);

    /// <summary>Renews the lock every <see cref="SyncLockSettings.RenewEvery"/> until disposed, then releases it.</summary>
    private sealed class Lease : ISyncLease
    {
        private readonly LeaseContext _context;
        private readonly CancellationTokenSource _lost = new();
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _renewing;
        private long _seqNo;
        private long _primaryTerm;

        public Lease(LeaseContext context, SyncLockInfo info, long seqNo, long primaryTerm)
        {
            _context = context;
            Info = info;
            _seqNo = seqNo;
            _primaryTerm = primaryTerm;
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
                // Only our own version of the lock: never delete a lock someone else holds now.
                DeleteRequest release = new(_context.Index, LockId) { IfSeqNo = _seqNo, IfPrimaryTerm = _primaryTerm };
                (await _context.Client.DeleteAsync(release, CancellationToken.None).ConfigureAwait(false))
                    .Ensure("Release the sync lock", 404, 409);
            }
            catch (SearchStoreException ex)
            {
                // Elasticsearch unreachable: the lease simply expires.
                _context.OnReleaseFailed(ex);
            }

            _lost.Dispose();
            _stop.Dispose();
        }

        private async Task RenewLoopAsync()
        {
            SyncLockSettings settings = _context.Settings;
            while (!_stop.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(settings.RenewEvery, _context.Clock, _stop.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return; // Disposed: normal end of the run.
                }

                DateTimeOffset now = _context.Clock.GetUtcNow();
                SyncLockInfo renewed = Info with { LeaseUntil = now + settings.Lease };
                try
                {
                    IndexRequest<SyncLockInfo> renewal = new(renewed, _context.Index, LockId)
                    {
                        IfSeqNo = _seqNo,
                        IfPrimaryTerm = _primaryTerm,
                    };
                    IndexResponse written = (await _context.Client.IndexAsync(renewal, _stop.Token)
                        .ConfigureAwait(false))
                        .Ensure("Renew the sync lock", 409);
                    if (written.Status() == 409)
                    {
                        _context.OnLost();
                        await _lost.CancelAsync().ConfigureAwait(false);
                        return;
                    }

                    Info = renewed;
                    _seqNo = written.SeqNo ?? _seqNo;
                    _primaryTerm = written.PrimaryTerm ?? _primaryTerm;
                }
                catch (OperationCanceledException)
                {
                    return; // Disposed while renewing.
                }
                catch (SearchStoreException ex)
                {
                    _context.OnRenewFailed(ex.Message);
                    if (now >= Info.LeaseUntil)
                    {
                        // Our lease is over: another process may legitimately take the lock now.
                        _context.OnLost();
                        await _lost.CancelAsync().ConfigureAwait(false);
                        return;
                    }
                }
            }
        }
    }
}
