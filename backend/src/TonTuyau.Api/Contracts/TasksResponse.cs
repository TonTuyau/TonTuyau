using TonTuyau.Application.Tasks;

namespace TonTuyau.Api.Contracts;

/// <summary>The Tasks page (contract v1.7, TonTuyau 1.4): the three tasks, and the run going on.</summary>
/// <param name="Lock">A run going on (in any process), or null: then any task can start.</param>
/// <param name="RunningHere">The id of the run going on in this backend (it can be canceled), or null.</param>
public sealed record TasksResponse(RunLock? Lock, string? RunningHere, IReadOnlyList<TaskResponse> Tasks)
{
    public static explicit operator TasksResponse(TasksView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new TasksResponse(
            view.Lock is null ? null : (RunLock)view.Lock,
            view.RunningHere,
            [.. view.Tasks.Select(t => (TaskResponse)t)]);
    }
}
