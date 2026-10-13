using TonTuyau.Application.RunLogs;

namespace TonTuyau.Application.Tasks;

/// <param name="Complete">
/// No more line will come: the run ended a few seconds ago (its last lines are written by then), or long before 1.4
/// (no log at all).
/// </param>
public sealed record RunLogView(IReadOnlyList<RunLogLine> Lines, bool Complete);
