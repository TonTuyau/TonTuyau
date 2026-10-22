namespace TonTuyau.Infrastructure.Streaming;

/// <summary>A fragment of a fragmented MP4: its bytes (a <c>moof</c> and its <c>mdat</c>), and when its video starts.</summary>
/// <param name="Time">The video's decode time at its start, in seconds; null when it has no video.</param>
internal sealed record Mp4Fragment(byte[] Bytes, double? Time);

/// <summary>
/// Reads the fragmented MP4 ffmpeg writes on its output (C3): first the init segment (<c>ftyp</c> and <c>moov</c>:
/// what every part needs before it), then a fragment per keyframe, each with its video's time (the <c>tfdt</c> of its
/// video track, in that track's time scale) to tell which part it belongs to.
/// </summary>
internal sealed class FragmentedMp4Reader(Stream input)
{
    /// <summary>A box bigger than this isn't a fragment of a video of ours: the stream is refused.</summary>
    public const long MaxBoxBytes = 512L * 1024 * 1024;

    private uint _videoTrack;
    private uint _videoScale;

    /// <returns>The init segment; null when the stream ended before it.</returns>
    /// <exception cref="InvalidDataException">Not a fragmented MP4 with a video track.</exception>
    public async Task<byte[]?> ReadInitAsync(CancellationToken cancellationToken)
    {
        using MemoryStream init = new();
        while (await ReadBoxAsync(cancellationToken).ConfigureAwait(false) is (string type, byte[] box))
        {
            init.Write(box);
            if (type != "moov")
            {
                continue;
            }

            Mp4Box moov = new("moov", 0, 8, box.Length);
            (uint Id, string Kind, uint TimeScale, Mp4Box Trak) video = Mp4Boxes.Tracks(box, moov)
                .FirstOrDefault(t => t.Kind == "vide");
            if (video.Kind is null || video.TimeScale == 0)
            {
                throw new InvalidDataException("The output has no video track.");
            }

            _videoTrack = video.Id;
            _videoScale = video.TimeScale;
            return init.ToArray();
        }

        return null;
    }

    /// <returns>The next fragment; null at the end of the stream.</returns>
    public async Task<Mp4Fragment?> ReadFragmentAsync(CancellationToken cancellationToken)
    {
        byte[]? moof = null;
        while (await ReadBoxAsync(cancellationToken).ConfigureAwait(false) is (string type, byte[] box))
        {
            if (type == "moof")
            {
                moof = box;
            }
            else if (type == "mdat" && moof is not null)
            {
                byte[] bytes = new byte[moof.Length + box.Length];
                moof.CopyTo(bytes, 0);
                box.CopyTo(bytes, moof.Length);
                return new Mp4Fragment(bytes, VideoTime(moof));
            }
        }

        return null;
    }

    /// <summary>The <c>tfdt</c> of the video track's <c>traf</c>, in seconds.</summary>
    private double? VideoTime(byte[] moof)
    {
        foreach (Mp4Box traf in Mp4Boxes.In(moof, 8, moof.Length))
        {
            if (traf.Type != "traf"
                || Mp4Boxes.Child(moof, traf, "tfhd") is not Mp4Box tfhd
                || Mp4Boxes.U32(moof, tfhd.ContentStart + 4) != _videoTrack
                || Mp4Boxes.Child(moof, traf, "tfdt") is not Mp4Box tfdt)
            {
                continue;
            }

            ulong decode = moof[tfdt.ContentStart] == 1
                ? Mp4Boxes.U64(moof, tfdt.ContentStart + 4)
                : Mp4Boxes.U32(moof, tfdt.ContentStart + 4);
            return decode / (double)_videoScale;
        }

        return null;
    }

    /// <returns>A whole box (its header included) and its type; null at the end of the stream.</returns>
    private async Task<(string Type, byte[] Box)?> ReadBoxAsync(CancellationToken cancellationToken)
    {
        byte[] header = new byte[16];
        int read = await input.ReadAtLeastAsync(header.AsMemory(0, 8), 8, throwOnEndOfStream: false, cancellationToken)
            .ConfigureAwait(false);
        if (read < 8)
        {
            return null;
        }

        long size = Mp4Boxes.U32(header, 0);
        string type = System.Text.Encoding.ASCII.GetString(header, 4, 4);
        int headerLength = 8;
        if (size == 1)
        {
            await input.ReadExactlyAsync(header.AsMemory(8, 8), cancellationToken).ConfigureAwait(false);
            size = (long)Mp4Boxes.U64(header, 8);
            headerLength = 16;
        }

        if (size < headerLength || size > MaxBoxBytes)
        {
            throw new InvalidDataException($"A box '{type}' of {size} bytes in the output.");
        }

        byte[] box = new byte[size];
        header.AsSpan(0, headerLength).CopyTo(box);
        await input.ReadExactlyAsync(box.AsMemory(headerLength), cancellationToken).ConfigureAwait(false);
        return (type, box);
    }
}
