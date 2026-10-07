using TonTuyau.Application.Library;

namespace TonTuyau.Application.UnitTests.Library;

public sealed class ArtSizesTests
{
    [Theory, CombinatorialData]
    public void A_version_is_never_as_wide_as_the_original_or_wider(
        [CombinatorialValues(1, 47, 48, 49, 96, 300, 360, 361, 480, 719, 720, 721, 900, 1280, 1281, 2560)]
        int originalWidth,
        [CombinatorialValues(ArtSubject.VideoThumbnail, ArtSubject.ChannelThumb, ArtSubject.ChannelBanner)]
        ArtSubject subject)
    {
        IReadOnlyList<int> widths = ArtSizes.For(subject);

        IReadOnlyList<int> targets = ArtSizes.Targets(originalWidth, widths);

        Assert.All(targets, w => Assert.True(w < originalWidth, $"{w} for an original {originalWidth} wide"));
        Assert.Equal(widths.Where(w => w < originalWidth), targets);
    }

    [Theory]
    [InlineData(1280, new[] { 360, 720 })]
    [InlineData(721, new[] { 360, 720 })]
    [InlineData(720, new[] { 360 })]
    [InlineData(480, new[] { 360 })]
    [InlineData(360, new int[0])]
    [InlineData(120, new int[0])]
    public void Video_thumbnails_get_the_sizes_below_their_width(int originalWidth, int[] expected) =>
        Assert.Equal(expected, ArtSizes.Targets(originalWidth, ArtSizes.VideoThumbnail));

    [Fact]
    public void Targets_are_sorted_once_each_and_positive()
    {
        Assert.Equal([48, 96, 320], ArtSizes.Targets(900, [320, 48, 96, 48, 0, -5]));
    }

    [Fact]
    public void The_sizes_are_the_ones_QA09_chose()
    {
        Assert.Equal([360, 720], ArtSizes.For(ArtSubject.VideoThumbnail));
        Assert.Equal([48, 96, 176, 320], ArtSizes.For(ArtSubject.ChannelThumb));
        Assert.Equal([1280], ArtSizes.For(ArtSubject.ChannelBanner));
        Assert.Empty(ArtSizes.For(ArtSubject.ChannelTvArt));
        Assert.Equal(80, ArtSizes.WebpQuality);
    }

    [Theory]
    [InlineData(1280, 720, 360, 203)] // 202.5: halves round away from zero
    [InlineData(1280, 720, 720, 405)]
    [InlineData(640, 360, 360, 203)]
    [InlineData(480, 270, 360, 203)]
    [InlineData(1280, 718, 360, 202)] // 201.94
    [InlineData(900, 900, 48, 48)]
    [InlineData(2560, 424, 1280, 212)]
    [InlineData(2560, 423, 1280, 212)] // 211.5
    [InlineData(10000, 10, 48, 1)] // 0.048: never 0
    [InlineData(1080, 1920, 360, 640)]
    public void The_height_keeps_the_shape_rounded_to_the_nearest_pixel(int width, int height, int target, int expected)
    {
        Assert.Equal(expected, ArtSizes.ResizedHeight(width, height, target));
    }

    [Theory, CombinatorialData]
    public void The_height_is_within_half_a_pixel_of_the_exact_one(
        [CombinatorialValues(480, 640, 1280, 1281, 1920, 2560, 3840)] int width,
        [CombinatorialValues(270, 359, 360, 423, 424, 720, 1080, 2160)] int height,
        [CombinatorialValues(48, 96, 176, 320, 360, 720, 1280)] int target)
    {
        double exact = height * (double)target / width;

        int rounded = ArtSizes.ResizedHeight(width, height, target);

        Assert.InRange(rounded, Math.Max(1, exact - 0.5), Math.Max(1, exact + 0.5));
    }

    [Theory]
    [InlineData(0, 720, 360)]
    [InlineData(1280, 0, 360)]
    [InlineData(1280, 720, 0)]
    public void Sizes_must_be_positive(int width, int height, int target)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ArtSizes.ResizedHeight(width, height, target));
    }
}
