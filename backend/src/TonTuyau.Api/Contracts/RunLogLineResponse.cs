using TonTuyau.Application.RunLogs;

namespace TonTuyau.Api.Contracts;

/// <summary>A line of a run's log.</summary>
/// <param name="Id">The line's position: the next page asks for the lines <c>after</c> the last one's.</param>
/// <param name="Level">information | warning | error.</param>
public sealed record RunLogLineResponse(long Id, DateTime At, RunLogLevel Level, string Message)
{
    public static explicit operator RunLogLineResponse(RunLogLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return new RunLogLineResponse(line.Id, line.At.UtcDateTime, line.Level, line.Message);
    }
}
