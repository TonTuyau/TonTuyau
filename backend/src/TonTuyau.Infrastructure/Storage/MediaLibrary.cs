using TonTuyau.Application.Library;
using TonTuyau.Application.Sync;

namespace TonTuyau.Infrastructure.Storage;

/// <summary>
/// The video folder: TubeArchivist's files, read as they are, and TonTuyau's downloads (2.0), written next to them.
/// A download goes to a folder of its own under <see cref="DownloadsFolder"/> first, then is moved to its place. What
/// TonTuyau writes there is group-writable (folders 775, files 664, 2.0): anyone in the share's group can move or delete
/// it; the folders it didn't make (TubeArchivist's) are left as they are.
/// </summary>
internal sealed class MediaLibrary(string root) : IMediaLibrary
{
    /// <summary>The downloads going on, in the media folder (hidden: not a channel's folder).</summary>
    public const string DownloadsFolder = ".tontuyau-downloads";

    public const UnixFileMode FolderMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
        | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
        | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    public const UnixFileMode FileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
        | UnixFileMode.GroupRead | UnixFileMode.GroupWrite
        | UnixFileMode.OtherRead;

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
        Writing(() =>
        {
            MakeFolder(Path.Combine(_root, DownloadsFolder));
            MakeFolder(folder);
        });
        return folder;
    }

    public void Place(string file, string mediaPath)
    {
        string target = FileFor(mediaPath);
        Writing(() =>
        {
            MakeFolder(Path.GetDirectoryName(target)!);
            File.Move(file, target, overwrite: true);
            SetMode(target, FileMode);
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

    public bool Delete(string mediaPath)
    {
        string file = FileFor(mediaPath);
        if (!File.Exists(file))
        {
            return false;
        }

        Writing(() => File.Delete(file));
        return true;
    }

    /// <summary>A folder TonTuyau makes is group-writable (whatever the umask); one that's there is left as it is.</summary>
    private static void MakeFolder(string folder)
    {
        if (Directory.Exists(folder))
        {
            return;
        }

        Directory.CreateDirectory(folder);
        SetMode(folder, FolderMode);
    }

    private static void SetMode(string path, UnixFileMode mode)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, mode);
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
                $"The media folder can't be written to ({_root}): the backend's user (TONTUYAU_UID and TONTUYAU_GID, "
                    + "1654 by default) needs to write there.",
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
