namespace TonTuyau.Application.YouTube;

/// <summary>The admin's yt-dlp panel (1.5).</summary>
/// <param name="Running">The version yt-dlp runs as now; null when it can't be run.</param>
/// <param name="Bundled">Whether that's the copy in the image (else one downloaded into the tools folder).</param>
/// <param name="Waiting">A version ready, waiting for the task going on to end; null when none.</param>
public sealed record YtdlpState(
    YtdlpChoice Choice,
    string? Running,
    bool Bundled,
    string? Waiting,
    DateTimeOffset? CheckedAt,
    string? Error);
