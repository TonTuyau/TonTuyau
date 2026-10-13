using TonTuyau.Application.Sync;
using TonTuyau.Application.Tasks;

namespace TonTuyau.Api.Contracts;

/// <summary>A task of the Tasks page (contract v1.7).</summary>
/// <param name="Task">incremental | full | applyDeletions.</param>
/// <param name="Schedule">Its cron, in the sync's time zone (<c>TONTUYAU_TZ</c>); null for applyDeletions.</param>
/// <param name="Next">When the scheduler runs it next; null when it won't (applyDeletions, the scheduler off).</param>
/// <param name="LastRun">Its latest run, going on or not; null when it never ran.</param>
/// <param name="Waiting">applyDeletions: the list waiting to be applied, or null.</param>
public sealed record TaskResponse(
    SyncMode Task,
    string? Schedule,
    DateTime? Next,
    RunResponse? LastRun,
    WaitingDeletions? Waiting)
{
    public static explicit operator TaskResponse(TaskView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new TaskResponse(
            view.Task,
            view.Schedule,
            view.Next?.UtcDateTime,
            view.LastRun is null ? null : (RunResponse)view.LastRun,
            view.Waiting is null ? null : (WaitingDeletions)view.Waiting);
    }
}
