using TonTuyau.Application.YouTube;

namespace TonTuyau.Application.Library;

/// <param name="MediaPath">Relative to the media root, e.g. <c>UCxxx/abc.mp4</c>. Validated: no traversal.</param>
/// <param name="MediaPathCorrected">True when TubeArchivist's media_url pointed to another channel's folder and
/// the path was rebuilt from the video's own channel (R4-2).</param>
/// <param name="ChannelName">Its channel's name (2.0: the channel's, read with the video).</param>
/// <param name="ThumbnailPath">
/// Its art path: TubeArchivist's cache (<c>/cache/videos/a/abc.jpg</c>), or TonTuyau's art volume after a refresh.
/// </param>
/// <param name="Categories">Its YouTube category (YouTube gives one); none when it has none.</param>
/// <param name="YouTube">Its state on YouTube (1.5), as TonTuyau checked it: not checked until it does.</param>
public sealed record Video(
    string Id,
    string ChannelId,
    string ChannelName,
    string Title,
    string Description,
    VideoType Type,
    DateTimeOffset Published,
    DateTimeOffset Downloaded,
    TimeSpan Duration,
    long MediaSize,
    string MediaPath,
    bool MediaPathCorrected,
    string ThumbnailPath,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Categories,
    VideoStats Stats,
    IReadOnlyList<MediaTrack> Tracks,
    YouTubeStatus YouTube)
{
    /// <summary>Whether visitors are told it's gone from YouTube: only when TonTuyau confirmed it.</summary>
    public bool GoneFromYouTube => YouTube.State == YouTubeState.Gone;
}

public sealed record VideoStats(long Views, long Likes, long Dislikes);
