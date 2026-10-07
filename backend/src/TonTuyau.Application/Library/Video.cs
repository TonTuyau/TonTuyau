namespace TonTuyau.Application.Library;

/// <param name="MediaPath">Relative to the media root, e.g. <c>UCxxx/abc.mp4</c>. Validated: no traversal.</param>
/// <param name="MediaPathCorrected">True when TubeArchivist's media_url pointed to another channel's folder and
/// the path was rebuilt from the video's own channel (R4-2).</param>
/// <param name="ThumbnailPath">TubeArchivist cache path, e.g. <c>/cache/videos/a/abc.jpg</c>.</param>
/// <param name="Active">False when the video is gone from YouTube but still archived.</param>
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
    IReadOnlyList<MediaTrack> Tracks);

public sealed record VideoStats(long Views, long Likes, long Dislikes);
