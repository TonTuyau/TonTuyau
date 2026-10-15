using TonTuyau.Application.YouTube;

namespace TonTuyau.Api.Hosting;

/// <summary>
/// At startup (1.5), in the background: which yt-dlp and Deno the backend reads YouTube with, in its log; a warning
/// when one can't be run (outside the image, for instance). The backend starts either way.
/// </summary>
internal sealed partial class YouTubeToolsCheck(IYouTubeClient youTube, ILogger<YouTubeToolsCheck> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        YouTubeTools tools = await youTube
            .GetToolsAsync(stoppingToken)
            .ConfigureAwait(false);
        if (tools.YtdlpVersion is null || tools.DenoVersion is null)
        {
            LogMissing(tools.YtdlpVersion ?? "missing", tools.DenoVersion ?? "missing");
        }
        else
        {
            LogTools(tools.YtdlpVersion, tools.DenoVersion);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "YouTube: yt-dlp {Ytdlp}, Deno {Deno}.")]
    private partial void LogTools(string ytdlp, string deno);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "YouTube can't be read: yt-dlp {Ytdlp}, Deno {Deno} (TONTUYAU_YTDLP_PATH, TONTUYAU_DENO_PATH).")]
    private partial void LogMissing(string ytdlp, string deno);
}
