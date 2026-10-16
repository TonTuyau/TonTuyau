using TonTuyau.Application.Sync;

namespace TonTuyau.Application.Tasks;

/// <summary>The Tasks page's tasks, and the run going on if any.</summary>
/// <param name="Lock">Who holds the sync lock (a run going on, in any process), or null.</param>
/// <param name="RunningHere">The run going on in this backend (it can be canceled from here), or null.</param>
/// <param name="OnHold">The refreshes on hold (1.5), the first to resume first.</param>
public sealed record TasksView(
    SyncLockInfo? Lock,
    string? RunningHere,
    IReadOnlyList<TaskView> Tasks,
    IReadOnlyList<RunView> OnHold);
