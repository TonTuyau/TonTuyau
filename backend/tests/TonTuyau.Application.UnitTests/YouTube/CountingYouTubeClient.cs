using TonTuyau.Application.Library;
using TonTuyau.Application.YouTube;

namespace TonTuyau.Application.UnitTests.YouTube;

/// <summary>A yt-dlp that only says its tools' versions, and counts how often it's asked.</summary>
internal sealed class CountingYouTubeClient : IYouTubeClient
{
    public int ToolsAsked { get; private set; }

    /// <summary>What the next <see cref="GetToolsAsync"/> answers.</summary>
    public YouTubeTools Tools { get; set; } = new("2026.08.19", "2.9.7");

    public Task<YouTubeTools> GetToolsAsync(CancellationToken cancellationToken)
    {
        ToolsAsked++;
        return Task.FromResult(Tools);
    }

    public Task<YouTubeLookup<YouTubeVideo>> GetVideoAsync(
        string videoId,
        YouTubeCommentsRequest? comments,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<YouTubeLookup<YouTubeChannel>> GetChannelAsync(string channelId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<YouTubeLookup<YouTubeDownload>> DownloadVideoAsync(
        string videoId,
        string format,
        string folder,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<YouTubeCookieCheck> CheckCookieAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<YouTubeLookup<IReadOnlyList<YouTubeListed>>> ListTabAsync(
        string channelId,
        ChannelTab tab,
        Func<YouTubeListed, bool> stopAt,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
