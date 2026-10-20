namespace TonTuyau.Application.Library;

/// <summary>A video's file as TonTuyau downloaded it (2.0, B3), for a video of the library whose file was missing.</summary>
/// <param name="MediaPath">Relative to the media root: <c>&lt;channel id&gt;/&lt;video id&gt;.mkv</c>.</param>
/// <param name="Size">In bytes.</param>
/// <param name="Downloaded">When it was downloaded.</param>
public sealed record VideoFile(
    string MediaPath,
    long Size,
    DateTimeOffset Downloaded,
    IReadOnlyList<MediaTrack> Tracks);
