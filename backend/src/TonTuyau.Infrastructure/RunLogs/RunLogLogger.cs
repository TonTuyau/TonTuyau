using Microsoft.Extensions.Logging;

namespace TonTuyau.Infrastructure.RunLogs;

/// <summary>A category's logger for <see cref="RunLogLoggerProvider"/>.</summary>
internal sealed class RunLogLogger(string category, RunLogLoggerProvider provider) : ILogger
{
    private readonly LogLevel _from = category.StartsWith("TonTuyau.", StringComparison.Ordinal)
        ? LogLevel.Information
        : LogLevel.Warning;

    // The logger factory keeps the scopes (ISupportExternalScope): nothing to do here.
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _from && logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        if (!IsEnabled(logLevel))
        {
            return;
        }

        string? runId = provider.CurrentRunId();
        if (runId is null)
        {
            return;
        }

        string message = formatter(state, exception);
        provider.Write(runId, logLevel, exception is null ? message : $"{message}\n{exception.Message}");
    }
}
