using TonTuyau.Application.YouTube;

namespace TonTuyau.Infrastructure.Library;

/// <summary>
/// A row of <c>channel_states</c> (2.0): a channel's state on YouTube, as TonTuyau checked it. Its key is the
/// channel's (<c>channel_id</c>, referencing <c>channels</c>), like <see cref="VideoStateRow"/>.
/// </summary>
public sealed class ChannelStateRow
{
    public required string ChannelId { get; set; }

    /// <summary>Stored as its name (<c>Gone</c>); <c>NotChecked</c> until TonTuyau checks it, whatever its source.</summary>
    public YouTubeState State { get; set; } = YouTubeState.NotChecked;

    public DateTimeOffset? CheckedAt { get; set; }

    public string? Reason { get; set; }

    /// <summary>When TonTuyau last read its metadata on YouTube; null: never (the import's are TubeArchivist's).</summary>
    public DateTimeOffset? RefreshedAt { get; set; }
}
