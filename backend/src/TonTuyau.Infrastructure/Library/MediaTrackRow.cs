using TonTuyau.Application.Library;

namespace TonTuyau.Infrastructure.Library;

/// <summary>A track of a video's file, in <c>videos.tracks</c> (JSON): width and height for video tracks only.</summary>
public sealed class MediaTrackRow
{
    /// <summary>Stored as its name (<c>Audio</c>).</summary>
    public MediaTrackKind Kind { get; set; }

    public required string Codec { get; set; }

    public long Bitrate { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }
}
