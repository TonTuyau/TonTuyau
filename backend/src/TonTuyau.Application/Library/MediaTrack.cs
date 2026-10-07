namespace TonTuyau.Application.Library;

public enum MediaTrackKind
{
    Video,
    Audio,
}

/// <summary>One track (TubeArchivist: "stream") inside a media file. Width/height only for video tracks.</summary>
public sealed record MediaTrack(MediaTrackKind Kind, string Codec, long Bitrate, int? Width, int? Height);
