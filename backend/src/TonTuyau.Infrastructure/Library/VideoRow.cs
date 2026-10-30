using TonTuyau.Application.Library;
using TonTuyau.Infrastructure.YouTube;

namespace TonTuyau.Infrastructure.Library;

/// <summary>
/// A row of <c>videos</c> (2.0): one video of the library, everything TonTuyau knows of it, one column each. The
/// import from TubeArchivist fills a row when it adds the video, and never writes it again; a refresh on YouTube
/// (and, later, a download) writes the same columns: what TonTuyau read last is what the row holds.
/// </summary>
public sealed class VideoRow
{
    public required string Id { get; set; }

    /// <summary>Where the video came from: TubeArchivist's library, or (from 2.0's downloads) TonTuyau's own.</summary>
    public LibraryOrigin Origin { get; set; }

    public required string ChannelId { get; set; }

    /// <summary>Its channel (<c>channel_id</c> references <c>channels</c>): always loaded with the row.</summary>
    public ChannelRow Channel { get; set; } = null!;

    public required string Title { get; set; }

    public required string Description { get; set; }

    /// <summary>Stored as its name (<c>Short</c>).</summary>
    public VideoType Type { get; set; }

    public DateTimeOffset Published { get; set; }

    public DateTimeOffset Downloaded { get; set; }

    public TimeSpan Duration { get; set; }

    public long MediaSize { get; set; }

    /// <summary>Relative to the media root, e.g. <c>UCxxx/abc.mp4</c>.</summary>
    public required string MediaPath { get; set; }

    public bool MediaPathCorrected { get; set; }

    /// <summary>
    /// Its art path: TubeArchivist's cache (<c>/cache/videos/a/abc.jpg</c>), or TonTuyau's art volume after a refresh.
    /// </summary>
    public required string ThumbnailPath { get; set; }

    public List<string> Tags { get; set; } = [];

    /// <summary>Its YouTube category (YouTube gives one per video); null: none.</summary>
    public int? CategoryId { get; set; }

    /// <summary>Always loaded with the row.</summary>
    public CategoryRow? Category { get; set; }

    public long Views { get; set; }

    public long Likes { get; set; }

    public long Dislikes { get; set; }

    /// <summary>How many comments YouTube counts; null until a refresh read it (TubeArchivist doesn't say).</summary>
    public long? CommentCount { get; set; }

    /// <summary>The file's tracks (JSON).</summary>
    public List<MediaTrackRow> Tracks { get; set; } = [];

    /// <summary>True when the import found no file on the media share.</summary>
    public bool MediaMissing { get; set; }

    /// <summary>
    /// Its file purged on its own (2.3.0): no sync downloads it again, the index leaves it out (the admin pages list it
    /// apart, to unblock it).
    /// </summary>
    public bool SyncBlocked { get; set; }

    /// <summary>What the art store holds for the thumbnail (JSON); null until it's recorded.</summary>
    public ArtVersionsRow? ThumbnailArt { get; set; }

    /// <summary>Its state on YouTube (1.5), in <c>video_states</c>: always loaded with the row.</summary>
    public VideoStateRow YouTube { get; set; } = null!;

    /// <summary>Its chapters, in order (JSON); none until refreshed, or when YouTube marks none.</summary>
    public List<VideoChapterRow> Chapters { get; set; } = [];

    /// <summary>When TonTuyau last read its comments (<c>video_comments</c>); null: never.</summary>
    public DateTimeOffset? CommentsReadAt { get; set; }
}
