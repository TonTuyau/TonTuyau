namespace TonTuyau.Application.YouTube;

/// <summary>What TonTuyau itself knows of a video (1.5, kept in PostgreSQL): its state, its metadata once refreshed.</summary>
public sealed record VideoFacts(string VideoId, YouTubeStatus Status, RefreshedVideo? Refreshed);
