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

/// <summary>Makes the resized WebP versions of copied art (QA09). CPU-bound: the sync runs several at once.</summary>
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

/// <summary>One line of a full run's deletion list.</summary>
/// <param name="Kind"><c>video</c> or <c>channel</c>.</param>
public sealed record DeletionEntry(
    string Kind,
    string Id,
    string Title,
    string? ChannelId,
    string? Type,
    DateTimeOffset? Downloaded,
    string? LastSeenRun)
{
    /// <summary>Copied art to remove with the item (TubeArchivist cache paths).</summary>
    public IReadOnlyList<string> Art { get; init; } = [];

    public bool Equals(DeletionEntry? other) =>
        other is not null
        && (Kind, Id, Title, ChannelId, Type, Downloaded, LastSeenRun)
            == (other.Kind, other.Id, other.Title, other.ChannelId, other.Type, other.Downloaded, other.LastSeenRun)
        && Art.SequenceEqual(other.Art);

    public override int GetHashCode() => HashCode.Combine(Kind, Id);
}

/// <summary>Deletion lists of full runs, as JSONL files under <c>TONTUYAU_SYNC_LOG_ROOT</c>, for a person to check.</summary>
public interface IDeletionLog
{
    /// <returns>The file written.</returns>
    Task<string> WriteAsync(string runId, IReadOnlyList<DeletionEntry> entries, CancellationToken cancellationToken);

    /// <returns>The run's list, or null when there's no file for it.</returns>
    Task<IReadOnlyList<DeletionEntry>?> ReadAsync(string runId, CancellationToken cancellationToken);
}
