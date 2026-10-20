using TonTuyau.Application.Library;

namespace TonTuyau.Infrastructure.Library;

/// <summary>
/// A row of <c>channel_subscriptions</c> (2.0, B4): a channel TonTuyau is subscribed to, and which of its tabs it
/// downloads. Its key is the channel's (<c>channel_id</c>, referencing <c>channels</c>), like
/// <see cref="ChannelStateRow"/>; no row: not subscribed.
/// </summary>
public sealed class ChannelSubscriptionRow
{
    public required string ChannelId { get; set; }

    public bool Videos { get; set; }

    public bool Shorts { get; set; }

    public bool Streams { get; set; }

    public DateTimeOffset SubscribedAt { get; set; }

    /// <summary>The account that subscribed it; null: taken from TubeArchivist at the import.</summary>
    public string? SubscribedBy { get; set; }

    /// <summary>Stored as its name (<c>TubeArchivist</c>), as <see cref="ChannelRow.Origin"/>.</summary>
    public LibraryOrigin Origin { get; set; }
}
