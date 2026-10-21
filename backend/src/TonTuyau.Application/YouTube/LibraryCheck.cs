namespace TonTuyau.Application.YouTube;

/// <summary>
/// A channel or video of the library as TonTuyau last checked it on YouTube (2.0, B8: what "Refresh everything"
/// orders and skips).
/// </summary>
/// <param name="ChannelId">A video's channel; null for a channel.</param>
/// <param name="CheckedAt">When TonTuyau last checked it; null: never.</param>
public sealed record LibraryCheck(string Id, string? ChannelId, YouTubeState State, DateTimeOffset? CheckedAt);
