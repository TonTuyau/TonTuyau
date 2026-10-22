using System.Globalization;
using System.Text;

namespace TonTuyau.Application.Streaming;

/// <summary>
/// How a video is played in parts (2.0, C3, question 9): its keyframes grouped into parts of about
/// <see cref="TargetSeconds"/>, each starting on a keyframe (ffmpeg copies them out of the file, it can't cut
/// elsewhere without re-encoding), and the HLS playlist listing them.
/// </summary>
public static class StreamPlan
{
    /// <summary>A part lasts at least this, from keyframe to keyframe (longer when the keyframes are further apart).</summary>
    public const double TargetSeconds = 6;

    /// <summary>The parts of a video of <paramref name="duration"/> seconds whose keyframes are at these times.</summary>
    /// <param name="keyframes">In seconds, in order; the first one at the video's start (or near it).</param>
    /// <returns>None when there's nothing to cut (no keyframe, or no length).</returns>
    public static IReadOnlyList<StreamPart> Parts(IReadOnlyList<double> keyframes, double duration)
    {
        ArgumentNullException.ThrowIfNull(keyframes);
        if (keyframes.Count == 0 || !(duration > 0))
        {
            return [];
        }

        List<double> starts = [Math.Max(0, keyframes[0])];
        foreach (double keyframe in keyframes.Skip(1))
        {
            if (keyframe >= duration)
            {
                break;
            }

            if (keyframe - starts[^1] >= TargetSeconds)
            {
                starts.Add(keyframe);
            }
        }

        return
        [
            .. starts.Select((start, index) => new StreamPart(
                index,
                start,
                (index + 1 < starts.Count ? starts[index + 1] : duration) - start)),
        ];
    }

    /// <summary>
    /// The playlist (HLS, version 7: fragmented MP4 parts after one init segment, a whole video known in advance).
    /// </summary>
    /// <param name="init">The init segment's address, relative to the playlist's.</param>
    /// <param name="part">A part's address from its index, relative to the playlist's.</param>
    public static string Playlist(IReadOnlyList<StreamPart> parts, string init, Func<int, string> part)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(part);
        StringBuilder text = new();
        int target = (int)Math.Ceiling(parts.Count == 0 ? TargetSeconds : parts.Max(p => p.Duration));
        text.Append("#EXTM3U\n#EXT-X-VERSION:7\n#EXT-X-PLAYLIST-TYPE:VOD\n#EXT-X-INDEPENDENT-SEGMENTS\n")
            .Append(CultureInfo.InvariantCulture, $"#EXT-X-TARGETDURATION:{target}\n")
            .Append("#EXT-X-MEDIA-SEQUENCE:0\n")
            .Append(CultureInfo.InvariantCulture, $"#EXT-X-MAP:URI=\"{init}\"\n");
        foreach (StreamPart each in parts)
        {
            text.Append(CultureInfo.InvariantCulture, $"#EXTINF:{each.Duration:0.000},\n{part(each.Index)}\n");
        }

        return text.Append("#EXT-X-ENDLIST\n").ToString();
    }
}
