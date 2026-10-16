namespace TonTuyau.Application.YouTube;

/// <summary>
/// The tools' versions (1.5), read when the backend starts and after each yt-dlp switch (by the updater), and kept: the
/// YouTube page reads them here instead of starting yt-dlp each time it opens (almost a second, more on a small
/// server: the program unpacks itself before it runs).
/// </summary>
public sealed class YouTubeToolsInUse(IYouTubeClient youTube)
{
    private YouTubeTools? _tools;

    /// <summary>The versions kept; read now if they haven't been yet (the backend just started).</summary>
    public async Task<YouTubeTools> GetAsync(CancellationToken cancellationToken) =>
        Volatile.Read(ref _tools) ?? await ReadAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>Reads them again (yt-dlp and Deno <c>--version</c>), and keeps them.</summary>
    public async Task<YouTubeTools> ReadAsync(CancellationToken cancellationToken)
    {
        YouTubeTools tools = await youTube
            .GetToolsAsync(cancellationToken)
            .ConfigureAwait(false);
        Volatile.Write(ref _tools, tools);
        return tools;
    }
}
