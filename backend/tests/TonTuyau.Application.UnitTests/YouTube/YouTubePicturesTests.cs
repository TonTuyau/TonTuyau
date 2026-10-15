using TonTuyau.Application.YouTube;

namespace TonTuyau.Application.UnitTests.YouTube;

public sealed class YouTubePicturesTests
{
    [Fact]
    public void A_video_keeps_its_largest_thumbnail()
    {
        YouTubeThumbnail[] offered =
        [
            new("https://i.ytimg.com/vi/x/default.jpg", 120, 90, "0"),
            new("https://i.ytimg.com/vi/x/unsized.jpg", null, null, "1"),
            new("https://i.ytimg.com/vi_webp/x/maxresdefault.webp", 1280, 720, "40"),
            new("https://i.ytimg.com/vi/x/hqdefault.jpg", 480, 360, "20"),
        ];

        Assert.Equal("https://i.ytimg.com/vi_webp/x/maxresdefault.webp", YouTubePictures.Thumbnail(offered));
        Assert.Null(YouTubePictures.Thumbnail([]));
    }

    [Fact]
    public void A_channel_keeps_its_uncropped_avatar_and_banner()
    {
        YouTubeThumbnail[] offered =
        [
            new("https://yt3.googleusercontent.com/banner=w2120", 2120, 351, "0"),
            new("https://yt3.googleusercontent.com/banner=s0", null, null, "banner_uncropped"),
            new("https://yt3.googleusercontent.com/avatar=s0", null, null, "avatar_uncropped"),
        ];

        Assert.Equal("https://yt3.googleusercontent.com/avatar=s0", YouTubePictures.Avatar(offered));
        Assert.Equal("https://yt3.googleusercontent.com/banner=s0", YouTubePictures.Banner(offered));
        Assert.Null(YouTubePictures.Avatar(offered[..1]));
    }

    [Theory]
    [InlineData("/cache/videos/k/k3Fq8xZ0aQw.jpg", "jpg", "/cache/videos/k/k3Fq8xZ0aQw.jpg")]
    [InlineData("/cache/videos/k/k3Fq8xZ0aQw.jpeg", "jpg", "/cache/videos/k/k3Fq8xZ0aQw.jpeg")]
    [InlineData("/cache/videos/k/k3Fq8xZ0aQw.jpg", "webp", "/cache/videos/k/k3Fq8xZ0aQw.webp")]
    [InlineData(null, "png", "/cache/videos/k/k3Fq8xZ0aQw.png")]
    public void A_picture_replaces_the_current_one_when_it_s_the_same_format(
        string? current,
        string extension,
        string expected) =>
        Assert.Equal(expected, YouTubePictures.PathFor(current, YouTubePictures.VideoStem("k3Fq8xZ0aQw"), extension));

    [Fact]
    public void A_channel_s_pictures_are_named_like_TubeArchivist_s()
    {
        Assert.Equal(
            "/cache/channels/UCx7Lm2QpB0vK9sTq4aZ1wYc_banner",
            YouTubePictures.ChannelStem("UCx7Lm2QpB0vK9sTq4aZ1wYc", "banner"));
    }
}
