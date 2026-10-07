using System.Text;
using TonTuyau.Application.Library;

namespace TonTuyau.Application.Media;

/// <summary>Content types of the files TonTuyau serves, and the playback type the front tests the browser with.</summary>
public static class MediaTypes
{
    /// <summary>Codec family (as TubeArchivist's ffprobe names it) → one representative RFC 6381 codec string.</summary>
    /// <remarks>
    /// Representative, not exact: TubeArchivist doesn't store profiles or levels. The strings are the common ones
    /// (AV1 Main 8-bit, VP9 profile 0 8-bit, H.264 High 4.0, HEVC Main), enough to tell "this browser can play this
    /// codec family" from "it can't" (R2-13).
    /// </remarks>
    public static readonly IReadOnlyDictionary<string, string> CodecStrings =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["av1"] = "av01.0.08M.08",
            ["vp9"] = "vp09.00.40.08",
            ["vp8"] = "vp8",
            ["h264"] = "avc1.640028",
            ["avc1"] = "avc1.640028",
            ["h265"] = "hvc1.1.6.L120.90",
            ["hevc"] = "hvc1.1.6.L120.90",
            ["opus"] = "opus",
            ["aac"] = "mp4a.40.2",
            ["mp3"] = "mp4a.6B",
            ["vorbis"] = "vorbis",
            ["flac"] = "flac",
        };

    /// <returns>The extension of a path, lower case, without the dot (<c>"mp4"</c>).</returns>
    public static string ExtensionOf(string path) =>
        Path.GetExtension(path)
            .TrimStart('.')
            .ToLowerInvariant();

    /// <summary>The Content-Type a media file is served with: what the file really is.</summary>
    public static string FileContentType(string extension) => extension switch
    {
        "mp4" or "m4v" => "video/mp4",
        "mov" => "video/quicktime",
        "webm" => "video/webm",
        "mkv" => "video/x-matroska",
        _ => "application/octet-stream",
    };

    /// <summary>The container part of the playback type: the closest type browsers know.</summary>
    public static string PlaybackContainer(string extension) => extension switch
    {
        "webm" or "mkv" => "video/webm",
        _ => "video/mp4",
    };

    /// <summary>
    /// The codec string for one track's codec. An unknown codec keeps its own name, so the browser answers "no"
    /// and the front shows the download button rather than a player that can't play.
    /// </summary>
    public static string CodecString(string codec)
    {
        ArgumentNullException.ThrowIfNull(codec);
        if (CodecStrings.TryGetValue(codec, out string? known))
        {
            return known;
        }

        StringBuilder safe = new();
        foreach (char c in codec.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_')
            {
                safe.Append(c);
            }
        }

        return safe.Length > 0 ? safe.ToString() : "unknown";
    }

    /// <summary>
    /// What the front passes to <c>canPlayType()</c> / <c>MediaSource.isTypeSupported()</c>, e.g.
    /// <c>video/mp4; codecs="av01.0.08M.08, opus"</c>. Video codecs first, then audio, each once.
    /// </summary>
    public static string PlaybackType(string extension, IReadOnlyList<MediaTrack> tracks)
    {
        ArgumentNullException.ThrowIfNull(tracks);
        string[] codecs = tracks
            .OrderBy(t => t.Kind == MediaTrackKind.Video ? 0 : 1)
            .Select(t => CodecString(t.Codec))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        string container = PlaybackContainer(extension);
        return codecs.Length == 0 ? container : $"{container}; codecs=\"{string.Join(", ", codecs)}\"";
    }

    /// <summary>The Content-Type of a copied image (TubeArchivist's art is JPEG, PNG or WebP).</summary>
    public static string ImageContentType(string extension) => extension switch
    {
        "jpg" or "jpeg" => "image/jpeg",
        "png" => "image/png",
        "webp" => "image/webp",
        _ => "application/octet-stream",
    };
}
