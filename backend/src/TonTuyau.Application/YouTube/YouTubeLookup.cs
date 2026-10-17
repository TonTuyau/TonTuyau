namespace TonTuyau.Application.YouTube;

/// <summary>The answer about one video or channel: what it is when <see cref="YouTubeAnswer.Found"/>.</summary>
/// <param name="Item">The metadata, when found; null otherwise.</param>
/// <param name="Reason">
/// Why it's gone or the check failed, as yt-dlp said it ("This video has been removed by the uploader"); null when found.
/// </param>
public sealed record YouTubeLookup<T>(YouTubeAnswer Answer, T? Item, string? Reason)
    where T : class;
