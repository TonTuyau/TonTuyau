using TonTuyau.Application.YouTube;

namespace TonTuyau.Infrastructure.Library;

/// <summary>
/// A row of <c>video_states</c> (2.0): a video's state on YouTube, as TonTuyau checked it. Its key is the video's
/// (<c>video_id</c>, referencing <c>videos</c>): one each, written and read with the video, deleted with it.
/// </summary>
public sealed class VideoStateRow
{
    public required string VideoId { get; set; }

    /// <summary>Stored as its name (<c>Gone</c>); <c>NotChecked</c> until TonTuyau checks it, whatever its source.</summary>
    public YouTubeState State { get; set; } = YouTubeState.NotChecked;

    public DateTimeOffset? CheckedAt { get; set; }

    public string? Reason { get; set; }

    /// <summary>When TonTuyau last read its metadata on YouTube; null: never (the import's are TubeArchivist's).</summary>
    public DateTimeOffset? RefreshedAt { get; set; }
}
