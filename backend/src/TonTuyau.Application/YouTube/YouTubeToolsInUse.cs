namespace TonTuyau.Application.YouTube;

/// <summary>
/// The tools' versions (1.5), read when the backend starts and after each yt-dlp switch (by the updater), and kept: the
/// YouTube page reads them here instead of starting yt-dlp each time it opens (almost a second, more on a small
/// server: the program unpacks itself before it runs). Kept with the yt-dlp they were read from (1.5.1): once another
/// one runs, they're read again, so the page never shows the one before a switch (the updater reads them only after
/// the switch, and that takes a second).
/// </summary>
public sealed class YouTubeToolsInUse(IYouTubeClient youTube, YtdlpLocator locator)
{
    private Kept? _kept;

    /// <summary>The versions kept; read now if they haven't been yet, or were read from another yt-dlp.</summary>
    public async Task<YouTubeTools> GetAsync(CancellationToken cancellationToken) =>
        Volatile.Read(ref _kept) is { } kept && kept.YtdlpPath == locator.Path
            ? kept.Tools
            : await ReadAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>Reads them again (yt-dlp and Deno <c>--version</c>), and keeps them.</summary>
    public async Task<YouTubeTools> ReadAsync(CancellationToken cancellationToken)
    {
        // The yt-dlp before it runs: when a switch comes meanwhile, the next read is done again (never the reverse).
        string path = locator.Path;
        YouTubeTools tools = await youTube
            .GetToolsAsync(cancellationToken)
            .ConfigureAwait(false);
        Volatile.Write(ref _kept, new Kept(path, tools));
        return tools;
    }

    private sealed record Kept(string YtdlpPath, YouTubeTools Tools);
}
