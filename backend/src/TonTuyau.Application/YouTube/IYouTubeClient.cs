namespace TonTuyau.Application.YouTube;

/// <summary>
/// Reads YouTube (1.5), through yt-dlp: a video's or a channel's metadata as it is now, and whether it's still there.
/// Never throws for YouTube's or the network's sake: a failure is a <see cref="YouTubeAnswer.CheckFailed"/> answer.
/// </summary>
public interface IYouTubeClient
{
    /// <param name="videoId">A YouTube video id (11 characters), already checked.</param>
    /// <param name="comments">Its comments too, as asked; null: none (much quicker).</param>
    Task<YouTubeLookup<YouTubeVideo>> GetVideoAsync(
        string videoId,
        YouTubeCommentsRequest? comments,
        CancellationToken cancellationToken);

    /// <param name="channelId">A YouTube channel id (<c>UC…</c>, 24 characters), already checked.</param>
    Task<YouTubeLookup<YouTubeChannel>> GetChannelAsync(string channelId, CancellationToken cancellationToken);

    Task<YouTubeTools> GetToolsAsync(CancellationToken cancellationToken);
}
