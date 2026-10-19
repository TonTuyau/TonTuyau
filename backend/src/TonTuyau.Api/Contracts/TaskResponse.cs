using TonTuyau.Application.Sync;
using TonTuyau.Application.Tasks;

namespace TonTuyau.Api.Contracts;

/// <summary>A task of the Tasks page (contract v1.7; v2.0: the TubeArchivist import, never scheduled).</summary>
/// <param name="Task">import.</param>
/// <param name="LastRun">Its latest run, going on or not; null when it never ran.</param>
/// <param name="Unavailable">Why it can't start (TubeArchivist isn't set up); null when it can.</param>
public sealed record TaskResponse(SyncMode Task, RunResponse? LastRun, string? Unavailable)
{
    public static explicit operator TaskResponse(TaskView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new TaskResponse(
            view.Task,
            view.LastRun is null ? null : (RunResponse)view.LastRun,
            view.Unavailable);
    }
}
