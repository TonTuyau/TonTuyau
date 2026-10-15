using TonTuyau.Application.YouTube;

namespace TonTuyau.Api.Hosting;

/// <summary>
/// Keeps the yt-dlp chosen running (1.5): at startup, the version made active last (from the tools folder, no network
/// needed) and the image's copy's version; then the choice applied (<see cref="YtdlpService.ApplyAsync"/>): now, once
/// a day, at once when the admin changes it, and every 30 seconds while a version waits for a task to end. The versions
/// in use go to the log; a warning when yt-dlp or Deno can't be run (outside the image, for instance).
/// </summary>
internal sealed partial class YtdlpUpdater(
    IServiceScopeFactory scopes,
    YtdlpLocator locator,
    IYtdlpInstaller installer,
    IYouTubeClient youTube,
    YtdlpUpdateSignal signal,
    ILogger<YtdlpUpdater> logger) : BackgroundService
{
    public static readonly TimeSpan CheckEvery = TimeSpan.FromDays(1);
    public static readonly TimeSpan WaitingCheckEvery = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        locator.BundledVersion = await installer
            .VersionOfAsync(locator.BundledPath, stoppingToken)
            .ConfigureAwait(false);
        if (installer.ReadActive() is { } active)
        {
            locator.Switch(installer.PathOf(active), active);
        }

        await LogToolsAsync(stoppingToken).ConfigureAwait(false);
        while (!stoppingToken.IsCancellationRequested)
        {
            string? before = locator.Version;
            YtdlpApplyResult result = await ApplyAsync(stoppingToken).ConfigureAwait(false);
            if (locator.Version != before)
            {
                await LogToolsAsync(stoppingToken).ConfigureAwait(false);
            }

            await signal
                .WaitAsync(result == YtdlpApplyResult.WaitingForTask ? WaitingCheckEvery : CheckEvery, stoppingToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>Applies the choice; anything unexpected (the database down…) is logged, and tried again later.</summary>
    private async Task<YtdlpApplyResult> ApplyAsync(CancellationToken stoppingToken)
    {
        try
        {
            AsyncServiceScope scope = scopes.CreateAsyncScope();
            await using (scope.ConfigureAwait(false))
            {
                return await scope.ServiceProvider
                    .GetRequiredService<YtdlpService>()
                    .ApplyAsync(stoppingToken)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogUnexpected(ex);
            return YtdlpApplyResult.Failed;
        }
    }

    private async Task LogToolsAsync(CancellationToken stoppingToken)
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
            LogTools(tools.YtdlpVersion, locator.Bundled ? "the image's" : "downloaded", tools.DenoVersion);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "yt-dlp: applying the version chosen failed.")]
    private partial void LogUnexpected(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "YouTube: yt-dlp {Ytdlp} ({Where}), Deno {Deno}.")]
    private partial void LogTools(string ytdlp, string where, string deno);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "YouTube can't be read: yt-dlp {Ytdlp}, Deno {Deno} (TONTUYAU_YTDLP_PATH, TONTUYAU_DENO_PATH).")]
    private partial void LogMissing(string ytdlp, string deno);
}
