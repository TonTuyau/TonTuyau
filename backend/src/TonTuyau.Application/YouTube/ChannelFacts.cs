namespace TonTuyau.Application.YouTube;

/// <summary>What TonTuyau itself knows of a channel (1.5, kept in PostgreSQL): its state, its metadata once refreshed.</summary>
public sealed record ChannelFacts(string ChannelId, YouTubeStatus Status, RefreshedChannel? Refreshed);
