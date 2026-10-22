namespace TonTuyau.Application.YouTube;

/// <summary>
/// A download that can't be done (2.0): YouTube says the video is gone or private, or yt-dlp didn't download it. The
/// run fails with the reason; YouTube refusing TonTuyau puts it on hold instead.
/// </summary>
public sealed class YouTubeDownloadException : Exception
{
    public YouTubeDownloadException()
    {
    }

    public YouTubeDownloadException(string message)
        : base(message)
    {
    }

    public YouTubeDownloadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Why, as yt-dlp said it ("Requested format is not available"), without "Not downloaded".</summary>
    public string Reason { get; private init; } = "";

    /// <summary>"Not downloaded: …", its <see cref="Reason"/> the reason alone.</summary>
    public static YouTubeDownloadException NotDownloaded(string reason) =>
        new($"Not downloaded: {reason}.") { Reason = reason };
}
