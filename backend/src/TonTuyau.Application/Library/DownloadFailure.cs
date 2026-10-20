namespace TonTuyau.Application.Library;

/// <summary>
/// A video a YouTube sync listed and couldn't download (2.0, B5): why, and how many times. Past
/// <see cref="SkippedAfter"/> tries the incremental sync leaves it; Try again, "Download everything" and the full sync
/// still try it. Gone once it's downloaded.
/// </summary>
/// <param name="Title">As its channel's tab listed it; null when it wasn't given.</param>
/// <param name="Reason">Why the last try failed, as yt-dlp said it.</param>
public sealed record DownloadFailure(
    string VideoId,
    string ChannelId,
    string? Title,
    string Reason,
    int Tries,
    DateTimeOffset FirstFailedAt,
    DateTimeOffset LastFailedAt)
{
    /// <summary>The incremental sync tries a video this many times (question 20), then leaves it.</summary>
    public const int SkippedAfter = 3;

    public bool Skipped => Tries >= SkippedAfter;
}
