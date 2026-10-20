using TonTuyau.Application.Library;
using TonTuyau.Application.Sync;

namespace TonTuyau.Infrastructure.Storage;

/// <summary>
/// The video folder: TubeArchivist's files, read as they are, and TonTuyau's downloads (2.0), written next to them.
/// A download goes to a folder of its own under <see cref="DownloadsFolder"/> first, then is moved to its place.
/// </summary>
internal sealed class MediaLibrary(string root) : IMediaLibrary
{
    /// <summary>The downloads going on, in the media folder (hidden: not a channel's folder).</summary>
    public const string DownloadsFolder = ".tontuyau-downloads";

    private readonly string _root = Path.GetFullPath(root);

    public MediaRootState CheckRoot()
    {
        if (!Directory.Exists(_root))
        {
            return MediaRootState.Missing;
        }

        // TonTuyau's own downloads folder doesn't make an empty share look mounted.
        return Directory
            .EnumerateFileSystemEntries(_root)
            .Any(entry => Path.GetFileName(entry) != DownloadsFolder)
            ? MediaRootState.Available
            : MediaRootState.Empty;
    }

    public bool Exists(string mediaPath) => File.Exists(FileFor(mediaPath));

    public StoredFile? Find(string mediaPath) => StoredFiles.Find(FileFor(mediaPath));

    public string NewDownloadFolder(string videoId)
    {
        if (!LibraryIds.IsVideoId(videoId))
        {
            throw new ArgumentException($"'{videoId}' isn't a video id.", nameof(videoId));
        }

        string folder = Path.Combine(_root, DownloadsFolder, $"{videoId}-{Guid.NewGuid():N}");
        Writing(() => Directory.CreateDirectory(folder));
        return folder;
    }

    public void Place(string file, string mediaPath)
    {
        string target = FileFor(mediaPath);
        Writing(() =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Move(file, target, overwrite: true);
        });
    }

    public void RemoveDownloadFolder(string folder)
    {
        string full = Path.GetFullPath(folder);
        string downloads = Path.Combine(_root, DownloadsFolder) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(downloads, StringComparison.Ordinal))
        {
            throw new ArgumentException($"'{folder}' isn't a download's folder.", nameof(folder));
        }

        if (Directory.Exists(full))
        {
            Directory.Delete(full, recursive: true);
        }
    }

    /// <summary>A write refused by the file system says so plainly: the VM's folder must let the backend write.</summary>
    private void Writing(Action write)
    {
        try
        {
            write();
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new IOException(
                $"The media folder can't be written to ({_root}): the backend's user (uid 1654) needs to write there.",
                ex);
        }
    }

    internal string FileFor(string mediaPath)
    {
        if (!LibraryPaths.IsMediaPath(mediaPath))
        {
            throw new ArgumentException($"'{mediaPath}' isn't a media path.", nameof(mediaPath));
        }

        string file = Path.GetFullPath(Path.Combine(_root, mediaPath));

        // Belt and braces, as for art: the format check already rules out "..".
        return file.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? file
            : throw new ArgumentException($"'{mediaPath}' resolves outside the media root.", nameof(mediaPath));
    }
}
