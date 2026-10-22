namespace TonTuyau.Application.Library;

/// <summary>
/// A channel TonTuyau is subscribed to (2.0, B4): B5's syncs download its videos, from these tabs. Unsubscribing
/// keeps the channel and its videos: only the syncs stop.
/// </summary>
/// <param name="Since">When it was subscribed (an import: when the import took it from TubeArchivist).</param>
/// <param name="By">
/// The account that subscribed it; null when not an account: taken from TubeArchivist at the import (question 13), or
/// added from the command line.
/// </param>
/// <param name="Origin">Where the subscription comes from: TubeArchivist's, or made in TonTuyau.</param>
/// <param name="SyncedAt">
/// When a YouTube sync (B5) last listed its tabs; null: never, so the next sync takes everything it has. TubeArchivist's
/// count as synced when the import takes them: TubeArchivist downloaded what it wanted of them.
/// </param>
public sealed record ChannelSubscription(
    string ChannelId,
    ChannelTabs Tabs,
    DateTimeOffset Since,
    string? By,
    LibraryOrigin Origin,
    DateTimeOffset? SyncedAt = null);
