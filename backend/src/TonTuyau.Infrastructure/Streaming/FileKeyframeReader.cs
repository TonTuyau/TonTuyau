using TonTuyau.Application.Streaming;

namespace TonTuyau.Infrastructure.Streaming;

/// <summary>
/// Reads a video file's keyframe times from its own index (C3): Matroska/WebM (TonTuyau's downloads) or MP4
/// (TubeArchivist's), told apart by their first bytes. A few reads of the file's index, never the whole file.
/// </summary>
internal sealed class FileKeyframeReader : IKeyframeReader
{
    public async Task<IReadOnlyList<double>?> ReadAsync(string fullPath, CancellationToken cancellationToken)
    {
        FileStream file = new(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        await using (file.ConfigureAwait(false))
        {
            byte[] start = new byte[8];
            int read = await file
                .ReadAtLeastAsync(start, 8, throwOnEndOfStream: false, cancellationToken)
                .ConfigureAwait(false);
            if (read < 8)
            {
                return null;
            }

            if (Mp4Boxes.U32(start, 0) == MatroskaKeyframeIndex.EbmlId)
            {
                return await MatroskaKeyframeIndex.ReadAsync(file, cancellationToken).ConfigureAwait(false);
            }

            string type = System.Text.Encoding.ASCII.GetString(start, 4, 4);
            return type is "ftyp" or "moov" or "free" or "mdat" or "wide" or "skip"
                ? await Mp4KeyframeIndex.ReadAsync(file, cancellationToken).ConfigureAwait(false)
                : null;
        }
    }
}
