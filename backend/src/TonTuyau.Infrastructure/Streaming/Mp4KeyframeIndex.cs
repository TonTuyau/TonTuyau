namespace TonTuyau.Infrastructure.Streaming;

/// <summary>
/// The keyframe times of an MP4 (C3: TubeArchivist's files), from its index alone: the <c>moov</c> box (at the start
/// or the end of the file) and, in its first video track, which samples are keyframes (<c>stss</c>; none: all are),
/// when each is decoded (<c>stts</c>), how much later it shows (<c>ctts</c>), and the edit list's shift
/// (<c>elst</c>). A fragmented MP4 (no samples in <c>moov</c>) has no such index: null.
/// </summary>
internal static class Mp4KeyframeIndex
{
    /// <summary>A <c>moov</c> bigger than this isn't read (an index of millions of samples: not a video of ours).</summary>
    public const int MaxMoovBytes = 64 * 1024 * 1024;

    public static async Task<IReadOnlyList<double>?> ReadAsync(FileStream file, CancellationToken cancellationToken)
    {
        byte[]? moov = await MoovAsync(file, cancellationToken).ConfigureAwait(false);
        return moov is null ? null : FromMoov(moov);
    }

    /// <summary>From a whole <c>moov</c> box (its header included).</summary>
    public static IReadOnlyList<double>? FromMoov(byte[] moov)
    {
        Mp4Box root = new("moov", 0, 8, moov.Length);
        (uint Id, string Kind, uint TimeScale, Mp4Box Trak) video = Mp4Boxes.Tracks(moov, root)
            .FirstOrDefault(t => t.Kind == "vide");
        if (video.Kind is null || video.TimeScale == 0
            || Mp4Boxes.Path(moov, video.Trak, "mdia", "minf", "stbl") is not Mp4Box stbl
            || Mp4Boxes.Child(moov, stbl, "stts") is not Mp4Box stts)
        {
            return null;
        }

        long[] decode = DecodeTimes(moov, stts);
        if (decode.Length == 0)
        {
            return null;
        }

        long[] shown = Offsets(moov, Mp4Boxes.Child(moov, stbl, "ctts"), decode.Length);
        double shift = EditShift(moov, root, video.Trak, video.TimeScale);
        IEnumerable<int> keyframes = Mp4Boxes.Child(moov, stbl, "stss") is Mp4Box stss
            ? SyncSamples(moov, stss)
            : Enumerable.Range(0, decode.Length);
        return
        [
            .. keyframes
                .Where(sample => sample < decode.Length)
                .Select(sample => Math.Max(0, ((decode[sample] + shown[sample]) / (double)video.TimeScale) - shift))
                .Order(),
        ];
    }

    /// <summary>Each sample's decode time, in the track's time scale: the durations of <c>stts</c> added up.</summary>
    private static long[] DecodeTimes(byte[] data, Mp4Box stts)
    {
        uint entries = Mp4Boxes.U32(data, stts.ContentStart + 4);
        List<long> times = [];
        long at = 0;
        for (uint i = 0; i < entries; i++)
        {
            int entry = stts.ContentStart + 8 + (int)(i * 8);
            if (entry + 8 > stts.End)
            {
                break;
            }

            uint count = Mp4Boxes.U32(data, entry);
            uint delta = Mp4Boxes.U32(data, entry + 4);
            for (uint n = 0; n < count; n++)
            {
                times.Add(at);
                at += delta;
            }
        }

        return [.. times];
    }

    /// <summary>How much later each sample shows than it's decoded (<c>ctts</c>); none: 0.</summary>
    private static long[] Offsets(byte[] data, Mp4Box? ctts, int samples)
    {
        long[] offsets = new long[samples];
        if (ctts is null)
        {
            return offsets;
        }

        bool signed = data[ctts.Value.ContentStart] == 1;
        uint entries = Mp4Boxes.U32(data, ctts.Value.ContentStart + 4);
        int sample = 0;
        for (uint i = 0; i < entries && sample < samples; i++)
        {
            int entry = ctts.Value.ContentStart + 8 + (int)(i * 8);
            if (entry + 8 > ctts.Value.End)
            {
                break;
            }

            uint count = Mp4Boxes.U32(data, entry);
            long offset = signed ? Mp4Boxes.I32(data, entry + 4) : Mp4Boxes.U32(data, entry + 4);
            for (uint n = 0; n < count && sample < samples; n++)
            {
                offsets[sample++] = offset;
            }
        }

        return offsets;
    }

    /// <summary>The sync samples' numbers (<c>stss</c>, from 1), as indices from 0.</summary>
    private static IEnumerable<int> SyncSamples(byte[] data, Mp4Box stss)
    {
        uint entries = Mp4Boxes.U32(data, stss.ContentStart + 4);
        for (uint i = 0; i < entries; i++)
        {
            int entry = stss.ContentStart + 8 + (int)(i * 4);
            if (entry + 4 > stss.End)
            {
                yield break;
            }

            yield return (int)Mp4Boxes.U32(data, entry) - 1;
        }
    }

    /// <summary>
    /// What the edit list takes off the track's times, in seconds: where the first edit starts in the media, less an
    /// empty edit before it (a delay, in the movie's time scale).
    /// </summary>
    private static double EditShift(byte[] data, Mp4Box moov, Mp4Box trak, uint trackScale)
    {
        if (Mp4Boxes.Path(data, trak, "edts", "elst") is not Mp4Box elst
            || Mp4Boxes.Child(data, moov, "mvhd") is not Mp4Box mvhd)
        {
            return 0;
        }

        bool mvhdV1 = data[mvhd.ContentStart] == 1;
        uint movieScale = Mp4Boxes.U32(data, mvhd.ContentStart + (mvhdV1 ? 20 : 12));
        bool v1 = data[elst.ContentStart] == 1;
        uint entries = Mp4Boxes.U32(data, elst.ContentStart + 4);
        double delay = 0;
        int size = v1 ? 20 : 12;
        for (uint i = 0; i < entries; i++)
        {
            int entry = elst.ContentStart + 8 + (int)(i * size);
            if (entry + size > elst.End)
            {
                break;
            }

            long duration = v1 ? (long)Mp4Boxes.U64(data, entry) : Mp4Boxes.U32(data, entry);
            long mediaTime = v1 ? Mp4Boxes.I64(data, entry + 8) : Mp4Boxes.I32(data, entry + 4);
            if (mediaTime == -1)
            {
                delay += movieScale == 0 ? 0 : duration / (double)movieScale;
                continue;
            }

            return (mediaTime / (double)trackScale) - delay;
        }

        return -delay;
    }

    /// <summary>The <c>moov</c> box, found among the file's top-level boxes; null when there's none.</summary>
    private static async Task<byte[]?> MoovAsync(FileStream file, CancellationToken cancellationToken)
    {
        byte[] header = new byte[16];
        long at = 0;
        while (at + 8 <= file.Length)
        {
            file.Position = at;
            await file.ReadExactlyAsync(header.AsMemory(0, 8), cancellationToken).ConfigureAwait(false);
            long size = Mp4Boxes.U32(header, 0);
            string type = System.Text.Encoding.ASCII.GetString(header, 4, 4);
            if (size == 1)
            {
                await file.ReadExactlyAsync(header.AsMemory(8, 8), cancellationToken).ConfigureAwait(false);
                size = (long)Mp4Boxes.U64(header, 8);
            }
            else if (size == 0)
            {
                size = file.Length - at;
            }

            if (size < 8 || at + size > file.Length)
            {
                return null;
            }

            if (type == "moov")
            {
                if (size > MaxMoovBytes)
                {
                    return null;
                }

                byte[] moov = new byte[size];
                file.Position = at;
                await file.ReadExactlyAsync(moov, cancellationToken).ConfigureAwait(false);
                return moov;
            }

            at += size;
        }

        return null;
    }
}
