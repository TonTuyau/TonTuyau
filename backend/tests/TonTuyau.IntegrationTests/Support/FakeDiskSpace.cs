using TonTuyau.Application.Sync;

namespace TonTuyau.IntegrationTests.Support;

/// <summary>
/// The videos folder's disk for the scenarios (2.2.0): never the machine's own, whose free space would decide whether
/// the downloads run. Roomy unless a scenario says otherwise; what the media folder gains after that is taken from its
/// free space, so the downloads fill it.
/// </summary>
public sealed class FakeDiskSpace : IDiskSpace
{
    public const long Gb = 1_000_000_000;

    private readonly Lock _gate = new();

    private DiskSpace? _set = new(4000 * Gb, 2000 * Gb);

    private long _filledThen;

    /// <summary>The media folder, once the backend has started.</summary>
    public string? Root { get; set; }

    /// <summary>The disk as it is now; the files the media folder has already aren't taken from it.</summary>
    public void Set(long total, long free)
    {
        lock (_gate)
        {
            _set = new DiskSpace(total, free);
            _filledThen = Filled();
        }
    }

    /// <summary>A disk that doesn't say its free space (a share that doesn't).</summary>
    public void Unknown()
    {
        lock (_gate)
        {
            _set = null;
        }
    }

    public DiskSpace? Read()
    {
        lock (_gate)
        {
            return _set is null
                ? null
                : _set with { Free = Math.Max(0, _set.Free - (Filled() - _filledThen)) };
        }
    }

    private long Filled() =>
        Root is null || !Directory.Exists(Root)
            ? 0
            : new DirectoryInfo(Root)
                .EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories = true })
                .Sum(f => f.Length);
}
