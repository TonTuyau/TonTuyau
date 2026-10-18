using TonTuyau.Application.Library;

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

    public List<string> Tags { get; set; } = [];

    /// <summary>Which YouTube tabs the channel has, by name (<c>Short</c>).</summary>
    public List<VideoType> Tabs { get; set; } = [];

    /// <summary>Art paths (TubeArchivist's cache, or TonTuyau's art volume after a refresh); null: none.</summary>
    public string? ThumbPath { get; set; }

    public string? BannerPath { get; set; }

    public string? TvArtPath { get; set; }

    public ArtVersionsRow? ThumbArt { get; set; }

    public ArtVersionsRow? BannerArt { get; set; }

    /// <summary>Its state on YouTube (1.5), in <c>channel_states</c>: always loaded with the row.</summary>
    public ChannelStateRow YouTube { get; set; } = null!;
}
