using TonTuyau.Application.Library;
using TonTuyau.Application.Sync;

namespace TonTuyau.Application.Dashboard;

/// <summary>The dashboard's storage (2.2.0): the videos folder's disk, TonTuyau's videos on it, the threshold.</summary>
/// <param name="Disk">The disk as its file system says it; null when it doesn't.</param>
/// <param name="Types">TonTuyau's videos by type: their files' sizes and how many, every type (0 when none).</param>
/// <param name="MinFree">The YouTube settings' threshold, in bytes: under it, a warning; 0: never.</param>
/// <param name="HoldDownloads">Below it, the downloads don't run.</param>
public sealed record StorageView(
    DiskSpace? Disk,
    IReadOnlyList<TypeStorageView> Types,
    long MinFree,
    bool HoldDownloads);

/// <param name="Bytes">Its files' sizes together.</param>
/// <param name="Count">How many videos of the type.</param>
public sealed record TypeStorageView(VideoType Type, long Bytes, long Count);
