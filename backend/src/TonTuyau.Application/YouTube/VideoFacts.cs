namespace TonTuyau.Application.YouTube;

/// <summary>What TonTuyau itself knows of a video (1.5, kept in PostgreSQL): its state, its metadata once refreshed.</summary>
/// <param name="ChannelId">The video's channel in the library.</param>
public sealed record VideoFacts(string VideoId, string ChannelId, YouTubeStatus Status, RefreshedVideo? Refreshed);
