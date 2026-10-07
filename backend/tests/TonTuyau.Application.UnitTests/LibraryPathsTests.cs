using TonTuyau.Application.Library;
using TonTuyau.Application.Sync;

namespace TonTuyau.Application.UnitTests;

public sealed class LibraryPathsTests
{
    private const string A = "UCaaaaaaaaaaaaaaaaaaaaaa";
    private const string B = "UCbbbbbbbbbbbbbbbbbbbbbb";
    private const string C = "UC-_cccccccccccccccccccc";

    [Theory]
    [InlineData("/cache/videos/a/abcdefghijk.jpg")]
    [InlineData("/cache/videos/_/_bcdefghijk.webp")]
    [InlineData("/cache/channels/UCaaaaaaaaaaaaaaaaaaaaaa_thumb.jpg")]
    [InlineData("/cache/channels/UCaaaaaaaaaaaaaaaaaaaaaa_banner.png")]
    [InlineData("/cache/channels/UCaaaaaaaaaaaaaaaaaaaaaa_tvart.jpeg")]
    public void Art_paths_from_TubeArchivist_are_accepted(string path) => Assert.True(LibraryPaths.IsArtPath(path));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/cache/videos/../../etc/abcdefghijk.jpg")]
    [InlineData("/cache/videos/a/../abcdefghijk.jpg")]
    [InlineData("/cache/videos/a/b/abcdefghijk.jpg")]
    [InlineData("/cache/videos/a/abcdefghijk.jpg/../../x.jpg")]
    [InlineData("/cache/videos/a/abcdefghijk.exe")]
    [InlineData("/cache/videos/a%2f..%2fabcdefghijk.jpg")]
    [InlineData("/cache/channels/UCaaaaaaaaaaaaaaaaaaaaaa_other.jpg")]
    [InlineData("/cache/channels/../UCaaaaaaaaaaaaaaaaaaaaaa_thumb.jpg")]
    [InlineData("/youtube/UCaaaaaaaaaaaaaaaaaaaaaa/abcdefghijk.mp4")]
    [InlineData("cache/videos/a/abcdefghijk.jpg")]
    [InlineData("/cache/videos/a/abcdefghijk.jpg\n")]
    public void Anything_else_is_not_an_art_path(string? path) => Assert.False(LibraryPaths.IsArtPath(path));

    [Theory]
    [InlineData("UCaaaaaaaaaaaaaaaaaaaaaa/abcdefghijk.mp4")]
    [InlineData("UCaaaaaaaaaaaaaaaaaaaaaa/abcdefghijk.webm")]
    public void Media_paths_are_accepted(string path) => Assert.True(LibraryPaths.IsMediaPath(path));

    [Theory]
    [InlineData("/UCaaaaaaaaaaaaaaaaaaaaaa/abcdefghijk.mp4")]
    [InlineData("UCaaaaaaaaaaaaaaaaaaaaaa/../abcdefghijk.mp4")]
    [InlineData("../UCaaaaaaaaaaaaaaaaaaaaaa/abcdefghijk.mp4")]
    [InlineData("UCaaaaaaaaaaaaaaaaaaaaaa/abcdefghijk.mp4.sh")]
    [InlineData("UCaaaaaaaaaaaaaaaaaaaaaa/sub/abcdefghijk.mp4")]
    public void Anything_else_is_not_a_media_path(string path) => Assert.False(LibraryPaths.IsMediaPath(path));

    [Fact]
    public void New_run_ids_are_valid_run_ids()
    {
        Assert.True(LibraryPaths.IsRunId(SyncRun.NewId(DateTimeOffset.UtcNow)));
    }

    [Theory]
    [InlineData("run-20261002T010000Z-1a2b3c/../../etc/passwd")]
    [InlineData("../run-20261002T010000Z-1a2b3c")]
    [InlineData("run-a")]
    [InlineData("")]
    public void Run_ids_from_the_command_line_are_checked(string id) => Assert.False(LibraryPaths.IsRunId(id));

    [Theory]
    [InlineData("/cache/videos/a/abcdefghijk.jpg", ArtSubject.VideoThumbnail, "abcdefghijk")]
    [InlineData("/cache/videos/_/_bcdefghijk.webp", ArtSubject.VideoThumbnail, "_bcdefghijk")]
    [InlineData("/cache/channels/UCaaaaaaaaaaaaaaaaaaaaaa_thumb.jpg", ArtSubject.ChannelThumb, A)]
    [InlineData("/cache/channels/UCbbbbbbbbbbbbbbbbbbbbbb_banner.png", ArtSubject.ChannelBanner, B)]
    [InlineData("/cache/channels/UC-_cccccccccccccccccccc_tvart.jpeg", ArtSubject.ChannelTvArt, C)]
    public void An_art_path_says_whose_image_it_is(string path, ArtSubject subject, string id)
    {
        Assert.Equal(new ArtOwner(subject, id), LibraryPaths.OwnerOf(path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/cache/videos/a/../abcdefghijk.jpg")]
    [InlineData("/cache/channels/UCaaaaaaaaaaaaaaaaaaaaaa_poster.jpg")]
    [InlineData("UCaaaaaaaaaaaaaaaaaaaaaa/abcdefghijk.mp4")]
    public void Anything_else_has_no_owner(string? path)
    {
        Assert.Null(LibraryPaths.OwnerOf(path));
    }
}
