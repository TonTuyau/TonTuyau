using TonTuyau.Application.RunLogs;

namespace TonTuyau.Infrastructure.RunLogs;

/// <summary>A row of the <c>run_log_lines</c> table. The application sees a <see cref="RunLogLine"/>.</summary>
public sealed class RunLogRow
{
    public long Id { get; set; }

    public required string RunId { get; set; }

    public DateTimeOffset At { get; set; }

    /// <summary><c>information</c>, <c>warning</c> or <c>error</c>.</summary>
    public required string Level { get; set; }

    public required string Message { get; set; }

    public static explicit operator RunLogLine(RunLogRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        RunLogLevel level = Enum.Parse<RunLogLevel>(row.Level, ignoreCase: true);
        return new RunLogLine(row.Id, row.RunId, row.At, level, row.Message);
    }

    /// <summary>A new row: the database gives its id.</summary>
    public static explicit operator RunLogRow(RunLogLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return new RunLogRow
        {
            RunId = line.RunId,
            At = line.At,
            Level = line.Level.ToString().ToLowerInvariant(),
            Message = line.Message,
        };
    }
}
