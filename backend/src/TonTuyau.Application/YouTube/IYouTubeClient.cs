using TonTuyau.Application.Library;

namespace TonTuyau.Application.YouTube;

/// <summary>
/// Reads YouTube (1.5), through yt-dlp: a video's or a channel's metadata as it is now, and whether it's still there;
/// signed in with the YouTube cookie when there is one (<see cref="IYouTubeCookieSource"/>).
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

    /// <param name="channel">
    /// A YouTube channel id (<c>UC…</c>, 24 characters), or its <c>@handle</c> (2.0, "Add a channel"), already checked:
    /// the answer then gives its id.
    /// </param>
    Task<YouTubeLookup<YouTubeChannel>> GetChannelAsync(string channel, CancellationToken cancellationToken);

    /// <summary>
    /// Downloads a video (2.0) into <paramref name="folder"/>, as one <c>.mkv</c> with its chapters and metadata inside,
    /// merged by ffmpeg when its video and audio come apart; the metadata read with it.
    /// </summary>
    /// <param name="videoId">A YouTube video id (11 characters), already checked.</param>
    /// <param name="format">What to download, in yt-dlp's syntax (<c>bestvideo+bestaudio</c>).</param>
    /// <param name="folder">An empty folder of the download's own, on the media folder's file system.</param>
    /// <param name="retryWithoutCookie">
    /// Signed in with the YouTube cookie, the format matching none of the video's: tried again at once without the
    /// cookie (2.2.1, the YouTube settings' <see cref="YouTubeSettings.RetryWithoutCookie"/>).
    /// </param>
    Task<YouTubeLookup<YouTubeDownload>> DownloadVideoAsync(
        string videoId,
        string format,
        string folder,
        bool retryWithoutCookie,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists a tab of a channel's page (2.0, B5: the YouTube syncs), newest first, as YouTube lists it, without reading
    /// each video. Stops as soon as <paramref name="stopAt"/> says so of an entry (the incremental sync: the first one the
    /// library has), that entry included.
    /// </summary>
    /// <param name="channelId">A YouTube channel id (<c>UC…</c>), already checked.</param>
    /// <returns>The entries read: none when the channel has no such tab; a refusal, gone… otherwise.</returns>
    Task<YouTubeLookup<IReadOnlyList<YouTubeListed>>> ListTabAsync(
        string channelId,
        ChannelTab tab,
        Func<YouTubeListed, bool> stopAt,
        CancellationToken cancellationToken);

    Task<YouTubeTools> GetToolsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Tries the stored cookie (<see cref="IYouTubeCookieSource"/>): reads the account's Liked videos with it, as
    /// TubeArchivist does.
    /// </summary>
    Task<YouTubeCookieCheck> CheckCookieAsync(CancellationToken cancellationToken);
}
