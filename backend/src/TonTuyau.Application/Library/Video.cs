using TonTuyau.Application.YouTube;

namespace TonTuyau.Application.Library;

/// <param name="MediaPath">Relative to the media root, e.g. <c>UCxxx/abc.mp4</c>. Validated: no traversal.</param>
/// <param name="MediaPathCorrected">True when TubeArchivist's media_url pointed to another channel's folder and
/// the path was rebuilt from the video's own channel (R4-2).</param>
/// <param name="ThumbnailPath">TubeArchivist cache path, e.g. <c>/cache/videos/a/abc.jpg</c>.</param>
/// <param name="Active">TubeArchivist's flag: false when its last refresh of the video failed (for any reason).</param>
/// <param name="YouTube">
/// TonTuyau's own knowledge (1.5): null until the sync laid its facts over TubeArchivist's (<see cref="YouTubeOverlay"/>).
/// </param>
public sealed record Video(
    string Id,
    string ChannelId,
    string ChannelName,
    string Title,
    string Description,
    VideoType Type,
    bool Active,
    DateTimeOffset Published,
    DateTimeOffset Downloaded,
    DateTimeOffset? LastRefreshed,
    TimeSpan Duration,
    long MediaSize,
    string MediaPath,
    bool MediaPathCorrected,
    string ThumbnailPath,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> PlaylistIds,
    VideoStats Stats,
    IReadOnlyList<MediaTrack> Tracks,
    YouTubeStatus? YouTube = null)
{
    /// <summary>The state as TonTuyau stands by it: its own, else from TubeArchivist's flag.</summary>
    public YouTubeStatus CurrentYouTube => YouTube ?? YouTubeStatus.FromTubeArchivist(Active);

    /// <summary>Whether visitors are told it's gone from YouTube: only when TonTuyau confirmed it.</summary>
    public bool GoneFromYouTube => CurrentYouTube.State == YouTubeState.Gone;
}

public sealed record VideoStats(long Views, long Likes, long Dislikes);
