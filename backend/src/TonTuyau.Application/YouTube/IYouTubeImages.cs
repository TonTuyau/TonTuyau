namespace TonTuyau.Application.YouTube;

/// <summary>
/// Downloads YouTube's pictures (1.5): thumbnails, avatars, banners, from YouTube's own picture hosts only. Never throws
/// for YouTube's or the network's sake.
/// </summary>
public interface IYouTubeImages
{
    /// <returns>The picture; null when it can't be had (another host, an error, too big, not a picture).</returns>
    Task<YouTubeImage?> GetAsync(string url, CancellationToken cancellationToken);
}
