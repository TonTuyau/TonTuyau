using TonTuyau.Application.Library;
using TonTuyau.Application.YouTube;

namespace TonTuyau.Infrastructure.Library;

/// <summary>
/// A row of <c>channels</c> (2.0): one channel of the library. Like <see cref="VideoRow"/>: TubeArchivist's columns,
/// written by a sync, and TonTuyau's own (<c>youtube_*</c>), written by a refresh; TonTuyau's win when it's read.
/// </summary>
public sealed class ChannelRow
{
    public required string Id { get; set; }

    public LibraryOrigin Origin { get; set; }

    public required string Name { get; set; }

    public required string Description { get; set; }

    public long Subscribers { get; set; }

    /// <summary>TubeArchivist's flag: false when its last refresh of the channel failed.</summary>
    public bool Active { get; set; }

    public DateTimeOffset? LastRefreshed { get; set; }

    public List<string> Tags { get; set; } = [];

    /// <summary>Which YouTube tabs the channel has, by name (<c>Short</c>).</summary>
    public List<VideoType> Tabs { get; set; } = [];

    /// <summary>TubeArchivist's cache paths; null when TubeArchivist has none.</summary>
    public string? ThumbPath { get; set; }

    public string? BannerPath { get; set; }

    public string? TvArtPath { get; set; }

    /// <summary>Hash of the columns a sync writes: an upsert with the same hash rewrites nothing.</summary>
    public required string ContentHash { get; set; }

    // The sync's bookkeeping: not content, a content rewrite keeps it.

    public string? SeenInRun { get; set; }

    public ArtVersionsRow? ThumbArt { get; set; }

    public ArtVersionsRow? BannerArt { get; set; }

    // What TonTuyau read on YouTube (1.5).

    /// <summary>Stored as its name (<c>Gone</c>); null: never checked (TubeArchivist's flag tells).</summary>
    public YouTubeState? YouTubeState { get; set; }

    public DateTimeOffset? YouTubeCheckedAt { get; set; }

    public string? YouTubeReason { get; set; }

    public DateTimeOffset? YouTubeRefreshedAt { get; set; }

    public string? YouTubeName { get; set; }

    public string? YouTubeDescription { get; set; }

    public long? YouTubeSubscribers { get; set; }

    public List<string> YouTubeTags { get; set; } = [];

    /// <summary>The pictures TonTuyau stored in its art volume; null: TubeArchivist's stay.</summary>
    public string? YouTubeThumbPath { get; set; }

    public string? YouTubeBannerPath { get; set; }
}
