namespace TonTuyau.Application.YouTube;

/// <summary>The yt-dlp choice as stored, and how the last check went (1.5).</summary>
/// <param name="CheckedAt">When the updater last looked at the choice (and GitHub for the latest).</param>
/// <param name="Error">Why the last check or install failed; null when it went well.</param>
public sealed record YtdlpSettings(YtdlpChoice Choice, DateTimeOffset? CheckedAt, string? Error);
