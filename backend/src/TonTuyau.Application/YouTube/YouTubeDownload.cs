using TonTuyau.Application.Library;

namespace TonTuyau.Application.YouTube;

/// <summary>A video yt-dlp downloaded (2.0): what YouTube says of it, and the file it left.</summary>
/// <param name="File">The downloaded file, in the folder it was asked to download into (<c>.mkv</c>).</param>
/// <param name="Size">The file's size, in bytes.</param>
/// <param name="Type">A video, a short or a stream, as YouTube has it.</param>
/// <param name="Tracks">The file's video and audio tracks, as yt-dlp chose them.</param>
public sealed record YouTubeDownload(
    YouTubeVideo Video,
    string File,
    long Size,
    VideoType Type,
    IReadOnlyList<MediaTrack> Tracks);
