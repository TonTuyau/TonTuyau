using Microsoft.Extensions.Logging;
using YtdlpLogType = ManuHub.Ytdlp.NET.LogType;

namespace TonTuyau.YouTube;

/// <summary>
/// One run's log, for Ytdlp.NET: it reports what yt-dlp writes to stderr here (as errors, warnings included), and
/// nowhere else. Those lines are kept for <see cref="YtdlpErrors"/>, and go to TonTuyau's log at the debug level (the
/// client logs the run's outcome itself).
/// </summary>
internal sealed partial class YtdlpLog(ILogger logger) : ManuHub.Ytdlp.NET.ILogger
{
    private readonly List<string> _errors = [];

    /// <summary>The run's stderr lines so far.</summary>
    public IReadOnlyList<string> Errors
    {
        get
        {
            lock (_errors)
            {
                return [.. _errors];
            }
        }
    }

    public void Log(YtdlpLogType type, string message)
    {
        if (type == YtdlpLogType.Error)
        {
            lock (_errors)
            {
                _errors.Add(message);
            }
        }

        LogLine(logger, type, message);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "yt-dlp ({Type}): {Message}")]
    private static partial void LogLine(ILogger logger, YtdlpLogType type, string message);
}
