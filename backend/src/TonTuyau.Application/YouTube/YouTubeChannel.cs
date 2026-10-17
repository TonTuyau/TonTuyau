namespace TonTuyau.Application.YouTube;

/// <summary>A channel as YouTube describes it now (1.5), read through yt-dlp.</summary>
/// <param name="Handle">Its <c>@handle</c>, when YouTube gives it.</param>
/// <param name="Thumbnails">Its avatar and banner pictures, as listed.</param>
public sealed record YouTubeChannel(
    string Id,
    string Name,
    string Description,
    string? Handle,
    long? Subscribers,
    IReadOnlyList<string> Tags,
    IReadOnlyList<YouTubeThumbnail> Thumbnails);
