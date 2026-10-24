using TonTuyau.Application.Dashboard;
using TonTuyau.Application.Library;

namespace TonTuyau.Api.Contracts;

/// <summary>The dashboard's storage (contract v2.12): the videos folder's disk, TonTuyau's videos on it.</summary>
/// <param name="Disk">The disk as its file system says it; null when it doesn't (a share that doesn't say).</param>
/// <param name="Types">Videos, shorts and streams, in that order (all three, even without any).</param>
/// <param name="MinFree">The YouTube settings' threshold, in bytes: under it, a warning; 0: never.</param>
/// <param name="HoldDownloads">Below it, the downloads don't run.</param>
public sealed record Storage(StorageDisk? Disk, IReadOnlyList<TypeStorage> Types, long MinFree, bool HoldDownloads)
{
    public static explicit operator Storage(StorageView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new Storage(
            view.Disk is null ? null : new StorageDisk(view.Disk.Total, view.Disk.Free),
            [.. view.Types.Select(t => new TypeStorage(t.Type, t.Bytes, t.Count))],
            view.MinFree,
            view.HoldDownloads);
    }
}

/// <param name="Total">The disk's size, in bytes.</param>
/// <param name="Free">What's free on it for TonTuyau, in bytes.</param>
public sealed record StorageDisk(long Total, long Free);

/// <param name="Bytes">The type's files' sizes together.</param>
/// <param name="Count">How many videos of the type.</param>
public sealed record TypeStorage(VideoType Type, long Bytes, long Count);
