using Microsoft.Extensions.Logging.Abstractions;
using TonTuyau.Application.Sync;
using TonTuyau.Infrastructure.Storage;

namespace TonTuyau.Infrastructure.UnitTests;

/// <summary>The videos folder's disk (2.2.0), read from the file system.</summary>
public sealed class DiskSpaceTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("tontuyau-disk-tests-").FullName;

    [Fact]
    public void A_folder_deep_in_a_disk_says_that_disks_size_and_free_space()
    {
        string deep = Directory.CreateDirectory(Path.Combine(_root, "a", "b")).FullName;

        DiskSpace? space = new MediaDiskSpace(deep, NullLogger<MediaDiskSpace>.Instance).Read();

        Assert.NotNull(space);
        DriveInfo temp = new(Path.GetTempPath());
        Assert.Equal(temp.TotalSize, space.Total);
        Assert.InRange(space.Free, 0, space.Total);
    }

    [Fact]
    public void A_folder_that_isnt_there_says_nothing()
    {
        DiskSpace? space = new MediaDiskSpace(Path.Combine(_root, "gone"), NullLogger<MediaDiskSpace>.Instance)
            .Read();

        Assert.Null(space);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
