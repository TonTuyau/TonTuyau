using TonTuyau.Application.Library;

namespace TonTuyau.Api.Contracts;

/// <summary>A video a YouTube sync couldn't download (contract v2.4).</summary>
/// <param name="Title">As its channel's tab listed it; null when it wasn't given.</param>
/// <param name="Reason">Why the last try failed, as yt-dlp said it.</param>
/// <param name="Tries">How many times it failed.</param>
/// <param name="Skipped">The incremental sync leaves it now (3 failures); Try again and the full sync still try it.</param>
/// <param name="YouTubeUrl">Its page on YouTube.</param>
public sealed record DownloadFailureResponse(
    string VideoId,
    string? Title,
    string Reason,
    int Tries,
    bool Skipped,
    DateTime FirstFailedAt,
    DateTime LastFailedAt,
    Uri YouTubeUrl)
{
    public static explicit operator DownloadFailureResponse(DownloadFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new DownloadFailureResponse(
            failure.VideoId,
            failure.Title,
            failure.Reason,
            failure.Tries,
            failure.Skipped,
            failure.FirstFailedAt.UtcDateTime,
            failure.LastFailedAt.UtcDateTime,
            new Uri($"https://www.youtube.com/watch?v={failure.VideoId}"));
    }
}
