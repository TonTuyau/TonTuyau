using System.Buffers.Binary;

namespace TonTuyau.Infrastructure.Streaming;

/// <summary>An MP4 box in a buffer: its type, and where its content is (after its header).</summary>
internal readonly record struct Mp4Box(string Type, int Start, int ContentStart, int End);

/// <summary>
/// Reading MP4 (ISO BMFF) boxes in memory (C3): the boxes of a range, a child by type, the big-endian numbers inside.
/// Only what the keyframe index and the parts need; a box that doesn't fit the buffer ends the walk.
/// </summary>
internal static class Mp4Boxes
{
    public static IEnumerable<Mp4Box> In(byte[] data, int start, int end)
    {
        int at = start;
        while (at + 8 <= end)
        {
            long size = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(at, 4));
            string type = System.Text.Encoding.ASCII.GetString(data, at + 4, 4);
            int header = 8;
            if (size == 1)
            {
                if (at + 16 > end)
                {
                    yield break;
                }

                size = (long)BinaryPrimitives.ReadUInt64BigEndian(data.AsSpan(at + 8, 8));
                header = 16;
            }
            else if (size == 0)
            {
                size = end - at;
            }

            if (size < header || at + size > end)
            {
                yield break;
            }

            yield return new Mp4Box(type, at, at + header, (int)(at + size));
            at += (int)size;
        }
    }

    public static Mp4Box? Child(byte[] data, Mp4Box parent, string type)
    {
        foreach (Mp4Box box in In(data, parent.ContentStart, parent.End))
        {
            if (box.Type == type)
            {
                return box;
            }
        }

        return null;
    }

    /// <summary>A child at a path of types (<c>mdia/minf/stbl</c>).</summary>
    public static Mp4Box? Path(byte[] data, Mp4Box parent, params string[] types)
    {
        Mp4Box? at = parent;
        foreach (string type in types)
        {
            if (at is null)
            {
                return null;
            }

            at = Child(data, at.Value, type);
        }

        return at;
    }

    public static uint U32(byte[] data, int at) => BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(at, 4));

    public static int I32(byte[] data, int at) => BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(at, 4));

    public static ulong U64(byte[] data, int at) => BinaryPrimitives.ReadUInt64BigEndian(data.AsSpan(at, 8));

    public static long I64(byte[] data, int at) => BinaryPrimitives.ReadInt64BigEndian(data.AsSpan(at, 8));

    /// <summary>A track of a <c>moov</c>: its id, its kind (<c>vide</c>, <c>soun</c>…) and its time scale.</summary>
    public static IEnumerable<(uint Id, string Kind, uint TimeScale, Mp4Box Trak)> Tracks(byte[] data, Mp4Box moov)
    {
        foreach (Mp4Box trak in In(data, moov.ContentStart, moov.End))
        {
            if (trak.Type != "trak"
                || Child(data, trak, "tkhd") is not Mp4Box tkhd
                || Path(data, trak, "mdia", "mdhd") is not Mp4Box mdhd
                || Path(data, trak, "mdia", "hdlr") is not Mp4Box hdlr)
            {
                continue;
            }

            // Full boxes: a version byte, then 3 bytes of flags; version 1 has 64-bit times before the id/scale.
            bool tkhdV1 = data[tkhd.ContentStart] == 1;
            uint id = U32(data, tkhd.ContentStart + (tkhdV1 ? 20 : 12));
            bool mdhdV1 = data[mdhd.ContentStart] == 1;
            uint scale = U32(data, mdhd.ContentStart + (mdhdV1 ? 20 : 12));
            string kind = System.Text.Encoding.ASCII.GetString(data, hdlr.ContentStart + 8, 4);
            yield return (id, kind, scale, trak);
        }
    }
}
