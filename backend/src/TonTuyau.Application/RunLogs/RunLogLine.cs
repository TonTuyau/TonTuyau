namespace TonTuyau.Application.RunLogs;

/// <summary>A line of a run's log.</summary>
/// <param name="Id">Given by the database as the lines are written (0 until then): their reading order.</param>
public sealed record RunLogLine(long Id, string RunId, DateTimeOffset At, RunLogLevel Level, string Message);
