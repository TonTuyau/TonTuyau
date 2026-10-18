using TonTuyau.Application.Library;
using TonTuyau.Application.YouTube;
using TonTuyau.Infrastructure.YouTube;

namespace TonTuyau.Infrastructure.Library;

/// <summary>
/// A row of <c>videos</c> (2.0): one video of the library, everything TonTuyau knows of it. What TubeArchivist brings
/// (its metadata, its file and tracks, its dates) and what TonTuyau read on YouTube (the <c>youtube_*</c> columns,
/// chapters) live in columns of their own: a sync writes only the first, a refresh only the second, so neither ever
/// overwrites the other; TonTuyau's values win when the video is read (<see cref="YouTubeOverlay"/>).
/// </summary>
public sealed class VideoRow
{
    public required string Id { get; set; }

    /// <summary>Where the video came from: TubeArchivist's library, or (from 2.0's downloads) TonTuyau's own.</summary>
    public LibraryOrigin Origin { get; set; }

    public required string ChannelId { get; set; }

    public required string ChannelName { get; set; }

    public required string Title { get; set; }

    public required string Description { get; set; }

    /// <summary>Stored as its name (<c>Short</c>).</summary>
    public VideoType Type { get; set; }

    /// <summary>TubeArchivist's flag: false when its last refresh of the video failed.</summary>
    public bool Active { get; set; }

    public DateTimeOffset Published { get; set; }

    public DateTimeOffset Downloaded { get; set; }

    public DateTimeOffset? LastRefreshed { get; set; }

    public TimeSpan Duration { get; set; }

    public long MediaSize { get; set; }

    /// <summary>Relative to the media root, e.g. <c>UCxxx/abc.mp4</c>.</summary>
    public required string MediaPath { get; set; }

    public bool MediaPathCorrected { get; set; }

    /// <summary>TubeArchivist's cache path, e.g. <c>/cache/videos/a/abc.jpg</c>.</summary>
    public required string ThumbnailPath { get; set; }

    public List<string> Tags { get; set; } = [];

    public List<string> Categories { get; set; } = [];

    public List<string> PlaylistIds { get; set; } = [];

    public long Views { get; set; }

    public long Likes { get; set; }

    public long Dislikes { get; set; }

    /// <summary>The file's tracks (JSON).</summary>
    public List<MediaTrackRow> Tracks { get; set; } = [];

    /// <summary>Hash of the columns a sync writes: an upsert with the same hash rewrites nothing.</summary>
    public required string ContentHash { get; set; }

    // The sync's bookkeeping: not content, a content rewrite keeps it.

    /// <summary>The last run that saw the video (its deletion planning: what a full run didn't see is gone).</summary>
    public string? SeenInRun { get; set; }

    /// <summary>True when the last full sync found no file on the media share.</summary>
    public bool MediaMissing { get; set; }

    /// <summary>What the art store holds for the thumbnail (JSON); null until the sync recorded it.</summary>
    public ArtVersionsRow? ThumbnailArt { get; set; }

    // What TonTuyau read on YouTube (1.5): null until it checked the video, the metadata until it refreshed it.

    /// <summary>Stored as its name (<c>Gone</c>); null: never checked (TubeArchivist's flag tells).</summary>
    public YouTubeState? YouTubeState { get; set; }

    public DateTimeOffset? YouTubeCheckedAt { get; set; }

    public string? YouTubeReason { get; set; }

    /// <summary>When TonTuyau last read its metadata: from then on, the <c>youtube_*</c> metadata wins.</summary>
    public DateTimeOffset? YouTubeRefreshedAt { get; set; }

    public string? YouTubeTitle { get; set; }

    public string? YouTubeChannelName { get; set; }

    public string? YouTubeDescription { get; set; }

    public List<string> YouTubeTags { get; set; } = [];

    public List<string> YouTubeCategories { get; set; } = [];

    public long? YouTubeViews { get; set; }

    public long? YouTubeLikes { get; set; }

    public long? YouTubeCommentCount { get; set; }

    /// <summary>The thumbnail TonTuyau stored in its art volume; null: TubeArchivist's stays.</summary>
    public string? YouTubeThumbnailPath { get; set; }

    /// <summary>Its chapters, in order (JSON); none until refreshed, or when YouTube marks none.</summary>
    public List<VideoChapterRow> Chapters { get; set; } = [];

    /// <summary>When TonTuyau last read its comments (<c>video_comments</c>); null: never.</summary>
    public DateTimeOffset? CommentsReadAt { get; set; }
}
