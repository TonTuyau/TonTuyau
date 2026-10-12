using Microsoft.Extensions.Logging;
using TonTuyau.Application.RunLogs;

namespace TonTuyau.Infrastructure.RunLogs;

/// <summary>
/// Copies to a run's log (1.4) the lines logged inside its <see cref="RunLogScope"/>, from any class: TonTuyau's own
/// from Information up, and the warnings and errors of everything else (not, say, every request the TubeArchivist
/// client sends). The other providers (the console: the backend's log) write them as always.
/// </summary>
[ProviderAlias("RunLog")]
internal sealed class RunLogLoggerProvider(RunLogWriter writer, TimeProvider clock)
    : ILoggerProvider, ISupportExternalScope
{
    private IExternalScopeProvider? _scopes;

    public ILogger CreateLogger(string categoryName) => new RunLogLogger(categoryName, this);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

    /// <returns>The run whose scope this line is logged in (the innermost one), or null outside any run.</returns>
    public string? CurrentRunId()
    {
        string? runId = null;
        _scopes?.ForEachScope<object?>(
            (scope, _) =>
            {
                if (scope is RunLogScope run)
                {
                    runId = run.RunId;
                }
            },
            null);
        return runId;
    }

    public void Write(string runId, LogLevel level, string message)
    {
        RunLogLevel runLevel = level switch
        {
            LogLevel.Warning => RunLogLevel.Warning,
            LogLevel.Error or LogLevel.Critical => RunLogLevel.Error,
            _ => RunLogLevel.Information,
        };
        writer.Add(new RunLogLine(0, runId, clock.GetUtcNow(), runLevel, message));
    }

    public void Dispose()
    {
        // Nothing of its own: the writer is a hosted service, stopped (and flushed) by the host.
    }
}
