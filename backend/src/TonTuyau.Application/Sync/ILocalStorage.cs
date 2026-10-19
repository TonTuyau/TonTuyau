using TonTuyau.Application.Library;

namespace TonTuyau.Application.Sync;

/// <summary>TonTuyau's own copy of TubeArchivist's art (thumbnails, channel art), under <c>TONTUYAU_ART_ROOT</c>.</summary>
public interface IArtStore
{
    /// <param name="cachePath">TubeArchivist cache path, e.g. <c>/cache/videos/a/abcdefghijk.jpg</c>.</param>
    bool Exists(string cachePath);

    /// <summary>Writes the file atomically (temp file + rename): a reader never sees half an image.</summary>
    Task SaveAsync(string cachePath, Stream content, CancellationToken cancellationToken);

    /// <summary>Deletes the copied image and its resized versions.</summary>
    void Delete(string cachePath);

    /// <returns>The copied file, or null when there's none (never copied, or deleted since).</returns>
    /// <exception cref="ArgumentException">Not an art path (see <c>LibraryPaths.IsArtPath</c>).</exception>
    StoredFile? Find(string cachePath);

    /// <returns>The resized version <paramref name="width"/> wide (WebP), or null when it wasn't made (yet).</returns>
    /// <exception cref="ArgumentException">Not an art path, or not a positive width.</exception>
    StoredFile? FindVersion(string cachePath, int width);

    /// <summary>
    /// What's on disk for one image, without resizing anything: the original's width (read from its header) and which
    /// of <paramref name="widths"/> already have a version.
    /// </summary>
    /// <returns>Null when the original isn't there or can't be read as an image.</returns>
    ArtVersions? Describe(string cachePath, IReadOnlyList<int> widths);
}

/// <summary>Makes the resized WebP versions of copied art (QA09). CPU-bound: the import runs several at once.</summary>
public interface IArtResizer
{
    /// <summary>
    /// Makes the versions of <paramref name="widths"/> that are missing and narrower than the original (never
    /// upscaled), from one decode of the original. Each file is written atomically.
    /// </summary>
    /// <returns>What exists afterwards, and how many versions this call made; null when the original isn't there.</returns>
    /// <exception cref="ArtResizeException">The original can't be decoded, or a version can't be encoded or written.</exception>
    ArtResizeResult? EnsureVersions(string cachePath, IReadOnlyList<int> widths);
}

/// <param name="Made">Versions written by this call (0 when they all existed).</param>
public sealed record ArtResizeResult(ArtVersions Versions, int Made);

/// <summary>An image couldn't be resized. <paramref name="Missing"/>: how many versions are still missing.</summary>
public sealed class ArtResizeException(string message, int missing, Exception? inner = null) : Exception(message, inner)
{
    public int Missing { get; } = missing;
}

/// <summary>A file on disk as the API serves it: where it is, how big, when it last changed.</summary>
public sealed record StoredFile(string FullPath, long Length, DateTimeOffset LastModified);

public enum MediaRootState
{
    Available,
    Missing,
    Empty,
}

/// <summary>The video files on the (read-only) NFS share, under <c>TONTUYAU_MEDIA_ROOT</c>.</summary>
public interface IMediaLibrary
{
    /// <summary>Missing or empty root = the share is probably not mounted: never conclude "every file is gone".</summary>
    MediaRootState CheckRoot();

    /// <param name="mediaPath">Relative to the media root, e.g. <c>UCxxx/abcdefghijk.mp4</c>.</param>
    bool Exists(string mediaPath);

    /// <returns>The file, or null when it isn't on the share (right now).</returns>
    /// <exception cref="ArgumentException">Not a media path (see <c>LibraryPaths.IsMediaPath</c>).</exception>
    StoredFile? Find(string mediaPath);
}
