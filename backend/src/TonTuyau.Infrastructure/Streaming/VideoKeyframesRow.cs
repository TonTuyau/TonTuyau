namespace TonTuyau.Infrastructure.Streaming;

/// <summary>
/// A video's keyframe times (2.0, C3, <c>video_keyframes</c>): read from its file's index at its first play, kept with
/// the file's size (another size: another file, read again). Milliseconds: enough for a part's start, half the room.
/// </summary>
public sealed class VideoKeyframesRow
{
    public required string VideoId { get; set; }

    public long FileSize { get; set; }

    /// <summary>Empty: the file has no index, it plays whole.</summary>
    public required int[] TimesMs { get; set; }
}
