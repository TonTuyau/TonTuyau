using TonTuyau.Application.Sync;

namespace TonTuyau.Infrastructure.Storage;

internal static class StoredFiles
{
    /// <returns>The file's size and date, or null when there's no regular file there.</returns>
    public static StoredFile? Find(string fullPath)
    {
        FileInfo file = new(fullPath);
        return file.Exists ? new StoredFile(file.FullName, file.Length, file.LastWriteTimeUtc) : null;
    }
}
