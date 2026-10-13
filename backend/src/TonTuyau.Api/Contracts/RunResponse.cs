using TonTuyau.Application.Sync;
using TonTuyau.Application.Tasks;

namespace TonTuyau.Api.Contracts;

/// <summary>A run of the history (contract v1.7, TonTuyau 1.4).</summary>
/// <param name="Task">incremental | full | applyDeletions.</param>
/// <param name="Trigger">What started it: scheduler | cli | startup (the index rebuild after an upgrade).</param>
/// <param name="State">running | succeeded | failed | canceled.</param>
/// <param name="Error">Why it failed; null otherwise.</param>
/// <param name="DeletionsPlanned">
/// A full run: the videos and channels gone from TubeArchivist it found; applying deletions: the list's size.
/// </param>
/// <param name="DeletionsBlocked">A full run over the limit: it deleted none, until its list is applied.</param>
/// <param name="AppliesTo">Applying deletions: the full run whose list it applied.</param>
/// <param name="IndexRebuild">It rebuilt the indices (after an upgrade, or asked for).</param>
/// <param name="Notes">What a person should know: escalations, lists over the limit, channels not cleaned…</param>
/// <param name="Cancellable">It's going on in this backend: <c>POST …/cancel</c> stops it (1.4).</param>
public sealed record RunResponse(
    string Id,
    SyncMode Task,
    SyncTrigger Trigger,
    SyncOutcome State,
    DateTime StartedAt,
    DateTime? FinishedAt,
    string? Error,
    RunCounts Counts,
    int DeletionsPlanned,
    bool DeletionsBlocked,
    string? AppliesTo,
    bool IndexRebuild,
    IReadOnlyList<string> Notes,
    bool Cancellable)
{
    public static explicit operator RunResponse(RunView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        SyncRun run = view.Run;
        return new RunResponse(
            run.Id,
            run.Mode,
            run.Trigger,
            run.Outcome,
            run.StartedAt.UtcDateTime,
            run.FinishedAt?.UtcDateTime,
            run.Error,
            (RunCounts)run.Counts,
            run.DeletionsPlanned,
            run.DeletionsBlockedByLimit,
            run.AppliesTo,
            run.IndexRebuild,
            run.Notes,
            view.Cancellable);
    }
}
