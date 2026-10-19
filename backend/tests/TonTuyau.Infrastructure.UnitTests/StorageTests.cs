using TonTuyau.Application.Sync;
using TonTuyau.Infrastructure.Storage;

namespace TonTuyau.Infrastructure.UnitTests;

public sealed class StorageTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "tontuyau-tests-" + Guid.NewGuid().ToString("N")[..8]);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/cache/videos/a/abcdefghijk.jpg", "videos/a/abcdefghijk.jpg")]
    [InlineData("/cache/channels/UCaaaaaaaaaaaaaaaaaaaaaa_banner.jpg", "channels/UCaaaaaaaaaaaaaaaaaaaaaa_banner.jpg")]
    public async Task Art_keeps_TubeArchivist_layout_under_the_root(string cachePath, string relative)
    {
        FileArtStore store = new(_root);
        using MemoryStream image = new([1, 2, 3]);

        await store.SaveAsync(cachePath, image, Ct);

        Assert.True(store.Exists(cachePath));
        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(Path.Combine(_root, relative), Ct));
    }

    [Fact]
    public async Task Saving_again_replaces_the_file_and_leaves_no_temp_files()
    {
        FileArtStore store = new(_root);
        using MemoryStream first = new([1, 1, 1, 1]);
        using MemoryStream second = new([2]);

        await store.SaveAsync("/cache/videos/a/abcdefghijk.jpg", first, Ct);
        await store.SaveAsync("/cache/videos/a/abcdefghijk.jpg", second, Ct);

        string[] files = Directory.GetFiles(Path.Combine(_root, "videos", "a"));
        Assert.Equal([2], await File.ReadAllBytesAsync(Assert.Single(files), Ct));
    }

    [Fact]
    public async Task Deleting_removes_the_file_and_missing_files_are_fine()
    {
        FileArtStore store = new(_root);
        using MemoryStream image = new([1]);
        await store.SaveAsync("/cache/videos/a/abcdefghijk.jpg", image, Ct);

        store.Delete("/cache/videos/a/abcdefghijk.jpg");
        store.Delete("/cache/videos/a/abcdefghijk.jpg");

        Assert.False(store.Exists("/cache/videos/a/abcdefghijk.jpg"));
    }

    [Fact]
    public void Deleting_in_a_folder_that_was_never_made_is_fine()
    {
        FileArtStore store = new(_root);

        store.Delete("/cache/videos/Y/YjzFStira-k.webp");

        Assert.False(store.Exists("/cache/videos/Y/YjzFStira-k.webp"));
    }

    [Theory]
    [InlineData("/cache/videos/../../../etc/abcdefghijk.jpg")]
    [InlineData("/etc/passwd")]
    [InlineData("/cache/videos/a/abcdefghijk.sh")]
    public void Art_paths_outside_the_rules_never_touch_the_disk(string cachePath)
    {
        FileArtStore store = new(_root);

        Assert.Throws<ArgumentException>(() => store.Exists(cachePath));
        Assert.Throws<ArgumentException>(() => store.Delete(cachePath));
        Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public void A_missing_media_root_is_reported_as_missing()
    {
        Assert.Equal(MediaRootState.Missing, new MediaLibrary(_root).CheckRoot());
    }

    [Fact]
    public void An_empty_media_root_is_reported_as_empty()
    {
        Directory.CreateDirectory(_root);

        Assert.Equal(MediaRootState.Empty, new MediaLibrary(_root).CheckRoot());
    }

    [Fact]
    public async Task Media_files_are_found_by_their_relative_path()
    {
        string folder = Path.Combine(_root, "UCaaaaaaaaaaaaaaaaaaaaaa");
        Directory.CreateDirectory(folder);
        await File.WriteAllBytesAsync(Path.Combine(folder, "abcdefghijk.mp4"), [0], Ct);
        MediaLibrary media = new(_root);

        Assert.Equal(MediaRootState.Available, media.CheckRoot());
        Assert.True(media.Exists("UCaaaaaaaaaaaaaaaaaaaaaa/abcdefghijk.mp4"));
        Assert.False(media.Exists("UCaaaaaaaaaaaaaaaaaaaaaa/bbbbbbbbbbb.mp4"));
        Assert.Throws<ArgumentException>(() => media.Exists("../etc/passwd"));
    }

    [Fact]
    public async Task Found_files_have_their_full_path_size_and_date()
    {
        string folder = Path.Combine(_root, "UCaaaaaaaaaaaaaaaaaaaaaa");
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, "abcdefghijk.webm");
        await File.WriteAllBytesAsync(file, [1, 2, 3], Ct);
        File.SetLastWriteTimeUtc(file, new DateTime(2026, 10, 1, 18, 0, 0, DateTimeKind.Utc));

        StoredFile? found = new MediaLibrary(_root).Find("UCaaaaaaaaaaaaaaaaaaaaaa/abcdefghijk.webm");

        Assert.Equal(
            new StoredFile(file, 3, new DateTimeOffset(2026, 10, 1, 18, 0, 0, TimeSpan.Zero)),
            found);
        Assert.Null(new MediaLibrary(_root).Find("UCaaaaaaaaaaaaaaaaaaaaaa/bbbbbbbbbbb.webm"));
    }

    [Fact]
    public async Task Copied_art_is_found_and_missing_art_is_not()
    {
        FileArtStore store = new(_root);
        using MemoryStream image = new([1, 2]);
        await store.SaveAsync("/cache/channels/UCaaaaaaaaaaaaaaaaaaaaaa_thumb.jpg", image, Ct);

        Assert.Equal(2, store.Find("/cache/channels/UCaaaaaaaaaaaaaaaaaaaaaa_thumb.jpg")?.Length);
        Assert.Null(store.Find("/cache/channels/UCaaaaaaaaaaaaaaaaaaaaaa_banner.jpg"));
    }

    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("UCaaaaaaaaaaaaaaaaaaaaaa/../../secret.mp4")]
    [InlineData("/etc/passwd")]
    [InlineData("UCaaaaaaaaaaaaaaaaaaaaaa/abcdefghijk.mp4/")]
    public void Paths_outside_the_rules_are_never_looked_up(string path)
    {
        Assert.Throws<ArgumentException>(() => new MediaLibrary(_root).Find(path));
        Assert.Throws<ArgumentException>(() => new FileArtStore(_root).Find(path));
    }


    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
