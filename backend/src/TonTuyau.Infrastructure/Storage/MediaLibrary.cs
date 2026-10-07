using TonTuyau.Application.Library;
using TonTuyau.Application.Sync;

namespace TonTuyau.Infrastructure.Storage;

/// <summary>Read-only view of the video share. TonTuyau never writes there.</summary>
internal sealed class MediaLibrary(string root) : IMediaLibrary
{
    private readonly string _root = Path.GetFullPath(root);

    public MediaRootState CheckRoot()
    {
        if (!Directory.Exists(_root))
        {
            return MediaRootState.Missing;
        }

        return Directory.EnumerateFileSystemEntries(_root).Any() ? MediaRootState.Available : MediaRootState.Empty;
    }

    public bool Exists(string mediaPath) => File.Exists(FileFor(mediaPath));

    public StoredFile? Find(string mediaPath) => StoredFiles.Find(FileFor(mediaPath));

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
