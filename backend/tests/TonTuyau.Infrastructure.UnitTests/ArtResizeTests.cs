using System.Buffers.Binary;
using System.Text;
using SkiaSharp;
using TonTuyau.Application.Library;
using TonTuyau.Application.Sync;
using TonTuyau.Infrastructure.Storage;

namespace TonTuyau.Infrastructure.UnitTests;

/// <summary>Resized art (QA09): the versions on disk, and how SkiaSharp makes them.</summary>
public sealed class ArtResizeTests : IDisposable
{
    private const string Video = "/cache/videos/a/abcdefghijk.jpg";
    private const string Thumb = "/cache/channels/UCaaaaaaaaaaaaaaaaaaaaaa_thumb.jpg";

    private static readonly SKColor Dark = new(40, 40, 40);

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "tontuyau-art-tests-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly FileArtStore _store;

    private readonly SkiaArtResizer _resizer;

    public ArtResizeTests()
    {
        _store = new FileArtStore(_root);
        _resizer = new SkiaArtResizer(_store);
    }

    [Fact]
    public void A_version_sits_next_to_its_original()
    {
        Assert.Equal(
            Path.Combine(Path.GetFullPath(_root), "videos", "a", "abcdefghijk.360.webp"),
            _store.VersionFor(Video, 360));
        Assert.Equal(
            Path.Combine(Path.GetFullPath(_root), "channels", "UCaaaaaaaaaaaaaaaaaaaaaa_thumb.48.webp"),
            _store.VersionFor(Thumb, 48));
    }

    [Theory]
    [InlineData("/cache/videos/../../../etc/abcdefghijk.jpg", 360)]
    [InlineData("/etc/passwd", 360)]
    public void Version_paths_follow_the_art_path_rules(string cachePath, int width)
    {
        Assert.Throws<ArgumentException>(() => _store.FindVersion(cachePath, width));
    }

    [Fact]
    public void Video_thumbnails_get_360_and_720_px_WebP_versions_with_the_same_shape()
    {
        WriteJpeg(Video, 1280, 720);

        ArtResizeResult? result = _resizer.EnsureVersions(Video, ArtSizes.VideoThumbnail);

        Assert.Equal(new ArtResizeResult(new ArtVersions(1280, [360, 720]), 2), result);
        AssertWebp(_store.VersionFor(Video, 360), 360, 203);
        AssertWebp(_store.VersionFor(Video, 720), 720, 405);
    }

    [Theory]
    [InlineData(480, 270, new[] { 360 })]
    [InlineData(720, 405, new[] { 360 })]
    [InlineData(360, 202, new int[0])]
    [InlineData(200, 112, new int[0])]
    public void Nothing_is_upscaled_or_made_at_the_original_width(int width, int height, int[] expected)
    {
        WriteJpeg(Video, width, height);

        ArtResizeResult? result = _resizer.EnsureVersions(Video, ArtSizes.VideoThumbnail);

        Assert.Equal(expected, result!.Versions.Widths);
        Assert.Equal(expected.Length, result.Made);
        Assert.Equal(width, result.Versions.OriginalWidth);
        foreach (int size in ArtSizes.VideoThumbnail.Except(expected))
        {
            Assert.False(File.Exists(_store.VersionFor(Video, size)), $"{size} made for an original {width} wide");
        }
    }

    [Fact]
    public void Existing_versions_are_kept_and_only_the_missing_ones_made()
    {
        WriteJpeg(Thumb, 900, 900);
        _resizer.EnsureVersions(Thumb, ArtSizes.ChannelThumb);
        File.Delete(_store.VersionFor(Thumb, 96));
        DateTime kept = File.GetLastWriteTimeUtc(_store.VersionFor(Thumb, 48));

        ArtResizeResult? again = _resizer.EnsureVersions(Thumb, ArtSizes.ChannelThumb);

        Assert.Equal(1, again!.Made);
        Assert.Equal([48, 96, 176, 320], again.Versions.Widths);
        Assert.Equal(kept, File.GetLastWriteTimeUtc(_store.VersionFor(Thumb, 48)));
    }

    [Fact]
    public void No_original_no_versions()
    {
        Assert.Null(_resizer.EnsureVersions(Video, ArtSizes.VideoThumbnail));
        Assert.Null(_store.Describe(Video, ArtSizes.VideoThumbnail));
    }

    [Fact]
    public void A_file_that_isnt_an_image_fails_with_the_count_of_missing_versions()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_store.FileFor(Video))!);
        File.WriteAllText(_store.FileFor(Video), "not an image");

        ArtResizeException ex = Assert.Throws<ArtResizeException>(
            () => _resizer.EnsureVersions(Video, ArtSizes.VideoThumbnail));

        Assert.Equal(2, ex.Missing);
        Assert.Null(_store.Describe(Video, ArtSizes.VideoThumbnail));
        string folder = Path.GetDirectoryName(_store.FileFor(Video))!;
        Assert.Equal(["abcdefghijk.jpg"], Directory.GetFiles(folder).Select(Path.GetFileName));
    }

    [Theory, CombinatorialData]
    public void The_bottom_row_and_right_column_are_not_a_blended_edge(
        [CombinatorialValues(1280, 1281)] int width,
        [CombinatorialValues(720, 718, 423)] int height)
    {
        WriteJpeg(Video, width, height, Dark);

        _resizer.EnsureVersions(Video, ArtSizes.VideoThumbnail);

        foreach (int size in ArtSizes.VideoThumbnail)
        {
            using SKBitmap version = SKBitmap.Decode(_store.VersionFor(Video, size));
            for (int x = 0; x < version.Width; x++)
            {
                AssertClose(Dark, version.GetPixel(x, version.Height - 1), $"{size} px, bottom row, x={x}");
            }

            for (int y = 0; y < version.Height; y++)
            {
                AssertClose(Dark, version.GetPixel(version.Width - 1, y), $"{size} px, right column, y={y}");
            }
        }
    }

    [Fact]
    public void Versions_carry_no_metadata()
    {
        WriteJpeg(Video, 1280, 720, exif: true);
        string original = Encoding.ASCII.GetString(File.ReadAllBytes(_store.FileFor(Video)));
        Assert.Contains("Exif", original, StringComparison.Ordinal);

        _resizer.EnsureVersions(Video, ArtSizes.VideoThumbnail);

        foreach (int size in ArtSizes.VideoThumbnail)
        {
            string[] chunks = RiffChunks(File.ReadAllBytes(_store.VersionFor(Video, size)));
            Assert.DoesNotContain("EXIF", chunks);
            Assert.DoesNotContain("ICCP", chunks);
            Assert.DoesNotContain("XMP ", chunks);
        }
    }

    [Fact]
    public void No_temporary_file_is_left_behind()
    {
        WriteJpeg(Video, 1280, 720);

        _resizer.EnsureVersions(Video, ArtSizes.VideoThumbnail);

        Assert.Equal(
            ["abcdefghijk.360.webp", "abcdefghijk.720.webp", "abcdefghijk.jpg"],
            Directory
                .GetFiles(Path.GetDirectoryName(_store.FileFor(Video))!)
                .Select(Path.GetFileName)
                .Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Describe_reads_the_width_and_the_versions_there_without_making_any()
    {
        WriteJpeg(Video, 1280, 720);
        Assert.Equal(new ArtVersions(1280, []), _store.Describe(Video, ArtSizes.VideoThumbnail));

        _resizer.EnsureVersions(Video, [360]);

        Assert.Equal(new ArtVersions(1280, [360]), _store.Describe(Video, ArtSizes.VideoThumbnail));
        Assert.False(File.Exists(_store.VersionFor(Video, 720)));
    }

    [Fact]
    public void Deleting_an_image_deletes_its_versions_and_nothing_else()
    {
        WriteJpeg(Video, 1280, 720);
        WriteJpeg("/cache/videos/a/abcdefghijx.jpg", 1280, 720);
        _resizer.EnsureVersions(Video, ArtSizes.VideoThumbnail);
        _resizer.EnsureVersions("/cache/videos/a/abcdefghijx.jpg", ArtSizes.VideoThumbnail);
        string folder = Path.GetDirectoryName(_store.FileFor(Video))!;
        File.WriteAllText(Path.Combine(folder, "abcdefghijk.notes.webp"), "not a version");

        _store.Delete(Video);

        Assert.Equal(
            ["abcdefghijk.notes.webp", "abcdefghijx.360.webp", "abcdefghijx.720.webp", "abcdefghijx.jpg"],
            Directory
                .GetFiles(folder)
                .Select(Path.GetFileName)
                .Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Many_images_can_be_resized_at_once()
    {
        string[] paths = Enumerable
            .Range(0, 12)
            .Select(i => $"/cache/videos/b/abcdefghi{i:D2}.jpg")
            .ToArray();
        foreach (string path in paths)
        {
            WriteJpeg(path, 640, 360);
        }

        ArtResizeResult?[] results = paths
            .AsParallel()
            .WithDegreeOfParallelism(6)
            .Select(p => _resizer.EnsureVersions(p, ArtSizes.VideoThumbnail))
            .ToArray();

        Assert.All(results, r => Assert.Equal(new ArtVersions(640, [360]), r!.Versions));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static void AssertWebp(string file, int width, int height)
    {
        byte[] bytes = File.ReadAllBytes(file);
        Assert.Equal("RIFF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal("WEBP", Encoding.ASCII.GetString(bytes, 8, 4));
        using SKCodec codec = SKCodec.Create(file);
        Assert.Equal((width, height), (codec.Info.Width, codec.Info.Height));
    }

    private static void AssertClose(SKColor expected, SKColor actual, string where)
    {
        const int Tolerance = 8;
        Assert.True(
            Math.Abs(expected.Red - actual.Red) <= Tolerance
                && Math.Abs(expected.Green - actual.Green) <= Tolerance
                && Math.Abs(expected.Blue - actual.Blue) <= Tolerance,
            $"{where}: {actual} instead of about {expected}");
    }

    private static string[] RiffChunks(byte[] webp)
    {
        List<string> chunks = [];
        int offset = 12;
        while (offset + 8 <= webp.Length)
        {
            chunks.Add(Encoding.ASCII.GetString(webp, offset, 4));
            int size = (int)BinaryPrimitives.ReadUInt32LittleEndian(webp.AsSpan(offset + 4, 4));
            offset += 8 + size + (size % 2);
        }

        return [.. chunks];
    }

    /// <summary>A JPEG, plain or with a colour gradient, optionally with an EXIF block (APP1) after the SOI marker.</summary>
    private void WriteJpeg(string cachePath, int width, int height, SKColor? solid = null, bool exif = false)
    {
        using SKBitmap bitmap = new(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using (SKCanvas canvas = new(bitmap))
        {
            if (solid is { } color)
            {
                canvas.Clear(color);
            }
            else
            {
                using SKPaint paint = new()
                {
                    Shader = SKShader.CreateLinearGradient(
                        new SKPoint(0, 0),
                        new SKPoint(width, height),
                        [SKColors.DarkBlue, SKColors.Orange],
                        SKShaderTileMode.Clamp),
                };
                canvas.DrawRect(0, 0, width, height, paint);
            }
        }

        using SKData data = bitmap.Encode(SKEncodedImageFormat.Jpeg, 95);
        byte[] jpeg = data.ToArray();
        if (exif)
        {
            byte[] payload = [.. "Exif\0\0"u8.ToArray(), .. "II*\0"u8.ToArray(), 8, 0, 0, 0, 0, 0];
            int length = payload.Length + 2;
            byte[] segment = [0xFF, 0xE1, (byte)(length >> 8), (byte)(length & 0xFF), .. payload];
            jpeg = [.. jpeg[..2], .. segment, .. jpeg[2..]];
        }

        string file = _store.FileFor(cachePath);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllBytes(file, jpeg);
    }
}
