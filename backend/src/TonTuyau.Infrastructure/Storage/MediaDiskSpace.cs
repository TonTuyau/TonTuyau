using Microsoft.Extensions.Logging;
using TonTuyau.Application.Sync;

namespace TonTuyau.Infrastructure.Storage;

/// <summary>
/// The videos folder's disk (2.2.0), as its file system says it (statvfs on Linux): the size and the space free for
/// TonTuyau's user, of the file system the folder is on, a share mounted there included.
/// </summary>
internal sealed partial class MediaDiskSpace(string root, ILogger<MediaDiskSpace> logger) : IDiskSpace
{
    public DiskSpace? Read()
    {
        try
        {
            DriveInfo drive = new(root);
            return drive.TotalSize > 0 ? new DiskSpace(drive.TotalSize, drive.AvailableFreeSpace) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            LogUnreadable(root, ex);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The free space of {Root}'s disk can't be read.")]
    private partial void LogUnreadable(string root, Exception error);
}
