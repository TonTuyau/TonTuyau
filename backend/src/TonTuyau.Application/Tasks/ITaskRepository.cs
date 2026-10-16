using TonTuyau.Application.Paging;
using TonTuyau.Application.Sync;

namespace TonTuyau.Application.Tasks;

/// <summary>The Tasks page's reads of the run history (Elasticsearch, <c>&lt;prefix&gt;-sync-runs</c>).</summary>
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

    /// <returns>Whether a run applied the deletion list of that full run, and succeeded.</returns>
    Task<bool> WasAppliedAsync(string fullRunId, CancellationToken cancellationToken);
}
