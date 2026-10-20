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
}
