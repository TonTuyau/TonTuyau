using TonTuyau.Application.Paging;
using TonTuyau.Application.Sync;

namespace TonTuyau.Application.Tasks;

/// <summary>The Tasks page's reads of the run history (2.0: <c>runs</c>, in PostgreSQL).</summary>
public interface ITaskRepository
{
    /// <returns>The latest run of that kind, whatever its outcome (running too), or null.</returns>
    Task<SyncRun?> GetLatestRunAsync(SyncMode mode, CancellationToken cancellationToken);

    /// <param name="mode">Null for every kind.</param>
    /// <param name="before">The previous page's last run: the runs that come after it, latest first.</param>
    /// <returns>
    /// The runs, latest first (started, then id), <paramref name="count"/> at most; NextCursor: the page's last id when
    /// more come.
    /// </returns>
    Task<ResultPage<SyncRun>> GetRunsAsync(
        SyncMode? mode,
        SyncRun? before,
        int count,
        CancellationToken cancellationToken);

    /// <returns>The refreshes on hold (1.5), the first to resume first.</returns>
    Task<IReadOnlyList<SyncRun>> GetOnHoldRunsAsync(CancellationToken cancellationToken);

    /// <returns>The runs waiting their turn (2.0), in the order they start: the first asked for first.</returns>
    Task<IReadOnlyList<SyncRun>> GetQueuedRunsAsync(CancellationToken cancellationToken);

    /// <returns>The runs of that kind not over yet (2.0, B7): queued, going on or on hold; the first asked for first.</returns>
    Task<IReadOnlyList<SyncRun>> GetPendingRunsAsync(SyncMode mode, CancellationToken cancellationToken);

}
