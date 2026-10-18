using TonTuyau.Application.Library;
using TonTuyau.Application.YouTube;

namespace TonTuyau.Infrastructure.Library;

/// <summary>
/// A row of <c>channels</c> (2.0): one channel of the library. Like <see cref="VideoRow"/>: one column each, filled by
/// the import when it adds the channel, then written by a refresh only.
/// </summary>
public sealed class ChannelRow
{
    public required string Id { get; set; }

    public LibraryOrigin Origin { get; set; }

    public required string Name { get; set; }

    public required string Description { get; set; }

    public long Subscribers { get; set; }

    public DateTimeOffset? LastRefreshed { get; set; }

    public List<string> Tags { get; set; } = [];

    /// <summary>Which YouTube tabs the channel has, by name (<c>Short</c>).</summary>
    public List<VideoType> Tabs { get; set; } = [];

    /// <summary>Art paths (TubeArchivist's cache, or TonTuyau's art volume after a refresh); null: none.</summary>
    public string? ThumbPath { get; set; }

    public string? BannerPath { get; set; }

    public string? TvArtPath { get; set; }

    public ArtVersionsRow? ThumbArt { get; set; }

    public ArtVersionsRow? BannerArt { get; set; }

    // Its state on YouTube (1.5), as TonTuyau checked it.

    /// <summary>Stored as its name (<c>Gone</c>); <c>NotChecked</c> until TonTuyau checks it, whatever its source.</summary>
    public YouTubeState YouTubeState { get; set; } = YouTubeState.NotChecked;

    public DateTimeOffset? YouTubeCheckedAt { get; set; }

    public string? YouTubeReason { get; set; }

    /// <summary>When TonTuyau last read its metadata on YouTube; null: never (the import's are TubeArchivist's).</summary>
    public DateTimeOffset? YouTubeRefreshedAt { get; set; }
}
