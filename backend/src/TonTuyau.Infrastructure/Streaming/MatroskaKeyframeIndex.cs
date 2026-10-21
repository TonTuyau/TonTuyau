namespace TonTuyau.Infrastructure.Streaming;

/// <summary>
/// The keyframe times of a Matroska or WebM file (C3: TonTuyau's downloads, <c>.mkv</c>), from its index alone: its
/// Cues (found through the SeekHead, or past the elements before them), the cue points of its video track, in the
/// segment's timestamp scale. A file without Cues has no such index: null.
/// </summary>
internal static class MatroskaKeyframeIndex
{
    public const uint EbmlId = 0x1A45DFA3;

    private const uint SegmentId = 0x18538067;
    private const uint SeekHeadId = 0x114D9B74;
    private const uint SeekId = 0x4DBB;
    private const uint SeekIdId = 0x53AB;
    private const uint SeekPositionId = 0x53AC;
    private const uint InfoId = 0x1549A966;
    private const uint TimestampScaleId = 0x2AD7B1;
    private const uint TracksId = 0x1654AE6B;
    private const uint TrackEntryId = 0xAE;
    private const uint TrackNumberId = 0xD7;
    private const uint TrackTypeId = 0x83;
    private const uint CuesId = 0x1C53BB6B;
    private const uint CuePointId = 0xBB;
    private const uint CueTimeId = 0xB3;
    private const uint CueTrackPositionsId = 0xB7;
    private const uint CueTrackId = 0xF7;
    private const uint ClusterId = 0x1F43B675;

    /// <summary>An element read whole (SeekHead, Info, Tracks, Cues) is never bigger than this.</summary>
    private const int MaxElementBytes = 32 * 1024 * 1024;

    private const ulong UnknownSize = ulong.MaxValue;

    public static async Task<IReadOnlyList<double>?> ReadAsync(FileStream file, CancellationToken cancellationToken)
    {
        file.Position = 0;
        (uint ebml, ulong ebmlSize, long ebmlData) = await HeaderAsync(file, cancellationToken).ConfigureAwait(false);
        if (ebml != EbmlId || ebmlSize == UnknownSize)
        {
            return null;
        }

        file.Position = ebmlData + (long)ebmlSize;
        (uint segment, ulong segmentSize, long segmentData) = await HeaderAsync(file, cancellationToken)
            .ConfigureAwait(false);
        if (segment != SegmentId)
        {
            return null;
        }

        long segmentEnd = segmentSize == UnknownSize
            ? file.Length
            : Math.Min(file.Length, segmentData + (long)segmentSize);
        ulong scale = 1_000_000;
        ulong? videoTrack = null;
        long? cuesAt = null;
        byte[]? cues = null;
        long at = segmentData;
        // The elements before the first cluster: SeekHead (where the Cues are), Info (the scale), Tracks.
        while (at < segmentEnd && cues is null)
        {
            file.Position = at;
            (uint id, ulong size, long data) = await HeaderAsync(file, cancellationToken).ConfigureAwait(false);
            if (id == 0 || size == UnknownSize)
            {
                break;
            }

            if (id is SeekHeadId or InfoId or TracksId or CuesId)
            {
                byte[]? content = await ContentAsync(file, data, size, cancellationToken).ConfigureAwait(false);
                if (content is null)
                {
                    return null;
                }

                switch (id)
                {
                    case SeekHeadId:
                        cuesAt ??= CuesPosition(content) is long relative ? segmentData + relative : null;
                        break;
                    case InfoId:
                        scale = Unsigned(content, TimestampScaleId) ?? scale;
                        break;
                    case TracksId:
                        videoTrack = VideoTrack(content);
                        break;
                    default:
                        cues = content;
                        break;
                }
            }
            else if (id == ClusterId && cuesAt is not null)
            {
                // The rest is clusters: the Cues are where the SeekHead says.
                break;
            }

            at = data + (long)size;
        }

        if (cues is null && cuesAt is not null)
        {
            file.Position = cuesAt.Value;
            (uint id, ulong size, long data) = await HeaderAsync(file, cancellationToken).ConfigureAwait(false);
            if (id == CuesId && size != UnknownSize)
            {
                cues = await ContentAsync(file, data, size, cancellationToken).ConfigureAwait(false);
            }
        }

        return cues is null || videoTrack is null ? null : CueTimes(cues, videoTrack.Value, scale);
    }

    /// <summary>The video track's cue points, in seconds, in order.</summary>
    private static List<double> CueTimes(byte[] cues, ulong track, ulong scale)
    {
        List<double> times = [];
        foreach ((uint id, int start, int end) in Children(cues, 0, cues.Length))
        {
            if (id != CuePointId)
            {
                continue;
            }

            ulong? time = Unsigned(cues, start, end, CueTimeId);
            bool ofVideo = Children(cues, start, end)
                .Where(child => child.Id == CueTrackPositionsId)
                .Any(positions => Unsigned(cues, positions.Start, positions.End, CueTrackId) == track);
            if (time is not null && ofVideo)
            {
                times.Add(time.Value * (double)scale / 1e9);
            }
        }

        times.Sort();
        return times;
    }

    private static ulong? VideoTrack(byte[] tracks)
    {
        foreach ((uint id, int start, int end) in Children(tracks, 0, tracks.Length))
        {
            if (id == TrackEntryId && Unsigned(tracks, start, end, TrackTypeId) == 1)
            {
                return Unsigned(tracks, start, end, TrackNumberId);
            }
        }

        return null;
    }

    /// <summary>Where the SeekHead says the Cues are, from the segment's data start.</summary>
    private static long? CuesPosition(byte[] seekHead)
    {
        foreach ((uint id, int start, int end) in Children(seekHead, 0, seekHead.Length))
        {
            if (id != SeekId)
            {
                continue;
            }

            (uint Id, int Start, int End) target = Children(seekHead, start, end).FirstOrDefault(c => c.Id == SeekIdId);
            ulong? position = Unsigned(seekHead, start, end, SeekPositionId);
            if (target.Id != 0 && position is not null && Unsigned(seekHead, target.Start, target.End) == CuesId)
            {
                return (long)position.Value;
            }
        }

        return null;
    }

    private static ulong? Unsigned(byte[] data, uint id) => Unsigned(data, 0, data.Length, id);

    /// <summary>The value of a child element, an unsigned integer.</summary>
    private static ulong? Unsigned(byte[] data, int start, int end, uint id)
    {
        foreach ((uint childId, int childStart, int childEnd) in Children(data, start, end))
        {
            if (childId == id)
            {
                return Unsigned(data, childStart, childEnd);
            }
        }

        return null;
    }

    private static ulong Unsigned(byte[] data, int start, int end)
    {
        ulong value = 0;
        for (int i = start; i < end && i - start < 8; i++)
        {
            value = (value << 8) | data[i];
        }

        return value;
    }

    /// <summary>The child elements of a range: each one's id, and where its data is.</summary>
    private static IEnumerable<(uint Id, int Start, int End)> Children(byte[] data, int start, int end)
    {
        int at = start;
        while (at < end)
        {
            int idLength = VintLength(data[at]);
            if (idLength is 0 or > 4 || at + idLength >= end)
            {
                yield break;
            }

            uint id = 0;
            for (int i = 0; i < idLength; i++)
            {
                id = (id << 8) | data[at + i];
            }

            int sizeAt = at + idLength;
            int sizeLength = VintLength(data[sizeAt]);
            if (sizeLength == 0 || sizeAt + sizeLength > end)
            {
                yield break;
            }

            ulong size = (ulong)(data[sizeAt] & (0xFF >> sizeLength));
            for (int i = 1; i < sizeLength; i++)
            {
                size = (size << 8) | data[sizeAt + i];
            }

            int dataStart = sizeAt + sizeLength;
            if (size > (ulong)(end - dataStart))
            {
                yield break;
            }

            yield return (id, dataStart, dataStart + (int)size);
            at = dataStart + (int)size;
        }
    }

    /// <summary>An element's header in the file, from where it is: its id, its size, where its data starts.</summary>
    private static async Task<(uint Id, ulong Size, long Data)> HeaderAsync(
        FileStream file,
        CancellationToken cancellationToken)
    {
        byte[] bytes = new byte[12];
        int read = await file.ReadAtLeastAsync(bytes, 2, throwOnEndOfStream: false, cancellationToken)
            .ConfigureAwait(false);
        int idLength = read > 0 ? VintLength(bytes[0]) : 0;
        if (idLength is 0 or > 4 || idLength >= read)
        {
            return (0, 0, 0);
        }

        uint id = 0;
        for (int i = 0; i < idLength; i++)
        {
            id = (id << 8) | bytes[i];
        }

        int sizeLength = VintLength(bytes[idLength]);
        if (sizeLength == 0 || idLength + sizeLength > read)
        {
            return (0, 0, 0);
        }

        ulong mask = (ulong)(0xFF >> sizeLength);
        ulong size = bytes[idLength] & mask;
        bool unknown = size == mask;
        for (int i = 1; i < sizeLength; i++)
        {
            size = (size << 8) | bytes[idLength + i];
            unknown &= bytes[idLength + i] == 0xFF;
        }

        return (id, unknown ? UnknownSize : size, file.Position - read + idLength + sizeLength);
    }

    private static async Task<byte[]?> ContentAsync(
        FileStream file,
        long data,
        ulong size,
        CancellationToken cancellationToken)
    {
        if (size > MaxElementBytes || data + (long)size > file.Length)
        {
            return null;
        }

        byte[] content = new byte[size];
        file.Position = data;
        await file.ReadExactlyAsync(content, cancellationToken).ConfigureAwait(false);
        return content;
    }

    /// <summary>How long an EBML variable-length number is, from its first byte (its leading zeros); 0: not one.</summary>
    private static int VintLength(byte first)
    {
        for (int length = 1; length <= 8; length++)
        {
            if ((first & (0x80 >> (length - 1))) != 0)
            {
                return length;
            }
        }

        return 0;
    }
}
