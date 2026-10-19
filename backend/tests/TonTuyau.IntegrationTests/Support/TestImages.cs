using System.Collections.Concurrent;
using SkiaSharp;
using TonTuyau.Application.Library;

namespace TonTuyau.IntegrationTests.Support;

/// <summary>
/// Real (small) JPEGs for the art store and the fake TubeArchivist, so the import can resize them (QA09). Each kind of
/// image has the shape of TubeArchivist's, smaller: thumbnails 800×450, channel pictures 400×400, banners 1600×265.
/// </summary>
public static class TestImages
{
    private static readonly ConcurrentDictionary<(int Width, int Height), byte[]> Cache = new();

    public static (int Width, int Height) DefaultSize(string cachePath) =>
        LibraryPaths.OwnerOf(cachePath)?.Subject switch
        {
            ArtSubject.VideoThumbnail => (800, 450),
            ArtSubject.ChannelThumb => (400, 400),
            ArtSubject.ChannelBanner => (1600, 265),
            _ => (160, 90),
        };

    public static byte[] Jpeg(int width, int height) => Cache.GetOrAdd((width, height), Make);

    private static byte[] Make((int Width, int Height) size)
    {
        using SKBitmap bitmap = new(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using (SKCanvas canvas = new(bitmap))
        {
            using SKPaint paint = new()
            {
                Shader = SKShader.CreateLinearGradient(
                    new SKPoint(0, 0),
                    new SKPoint(size.Width, size.Height),
                    [SKColors.DarkSlateBlue, SKColors.Gold],
                    SKShaderTileMode.Clamp),
            };
            canvas.DrawRect(0, 0, size.Width, size.Height, paint);
        }

        using SKData data = bitmap.Encode(SKEncodedImageFormat.Jpeg, 85);
        return data.ToArray();
    }
}
