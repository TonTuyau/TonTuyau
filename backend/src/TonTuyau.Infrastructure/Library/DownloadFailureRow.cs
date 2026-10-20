namespace TonTuyau.Infrastructure.Library;

/// <summary>
/// A row of <c>download_failures</c> (2.0, B5): a video a YouTube sync couldn't download. Its key is the video's id
/// (not in <c>videos</c>: it isn't downloaded); <c>channel_id</c> references <c>channels</c>, deleted with it.
/// </summary>
public sealed class DownloadFailureRow
{
    public required string VideoId { get; set; }

    public required string ChannelId { get; set; }

    public string? Title { get; set; }

    public required string Reason { get; set; }

    public int Tries { get; set; }

    public DateTimeOffset FirstFailedAt { get; set; }

    public DateTimeOffset LastFailedAt { get; set; }
}
