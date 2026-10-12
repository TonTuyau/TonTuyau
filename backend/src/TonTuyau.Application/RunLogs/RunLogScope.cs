namespace TonTuyau.Application.RunLogs;

/// <summary>
/// A logging scope (<c>ILogger.BeginScope</c>) around a run: every line logged inside it, from any class, is also
/// written to that run's log (1.4), besides the backend's own log as always.
/// </summary>
public sealed record RunLogScope(string RunId);
