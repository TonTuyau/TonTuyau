using TonTuyau.Application.YouTube;

namespace TonTuyau.Infrastructure.YouTube;

/// <summary>
/// A row of <c>youtube_channels</c> (1.5): what TonTuyau itself knows of a channel, by its YouTube id. The metadata
/// columns are set once TonTuyau refreshed the channel.
/// </summary>
public sealed class YouTubeChannelRow
{
    public required string ChannelId { get; set; }

    /// <summary>Stored as its name (<c>Gone</c>).</summary>
    public YouTubeState State { get; set; }

    public DateTimeOffset? CheckedAt { get; set; }

    public string? Reason { get; set; }

    public DateTimeOffset? RefreshedAt { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public long? Subscribers { get; set; }

    public List<string> Tags { get; set; } = [];

    public string? ThumbPath { get; set; }

    public string? BannerPath { get; set; }

    public static explicit operator ChannelFacts(YouTubeChannelRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        YouTubeStatus status = new(row.State, row.CheckedAt, row.Reason, row.RefreshedAt);
        RefreshedChannel? refreshed = row.RefreshedAt is null || row.Name is null
            ? null
            : new RefreshedChannel(
                row.Name,
                row.Description ?? "",
                row.Subscribers ?? 0,
                row.Tags,
                row.ThumbPath,
                row.BannerPath);
        return new ChannelFacts(row.ChannelId, status, refreshed);
    }
}
