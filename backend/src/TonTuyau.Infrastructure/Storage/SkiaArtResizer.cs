using SkiaSharp;
using TonTuyau.Application.Library;
using TonTuyau.Application.Sync;

namespace TonTuyau.Infrastructure.Storage;

/// <summary>
/// Resized WebP versions with SkiaSharp (QA09): one decode of the original for all widths, Mitchell cubic sampling,
/// quality 80, no metadata (no EXIF, ICC or XMP chunk is written), never upscaled, each file written atomically.
/// Thread-safe: the import runs several at once.
/// </summary>
internal sealed class SkiaArtResizer(FileArtStore store) : IArtResizer
{
    private static readonly SKSamplingOptions Sampling = new(SKCubicResampler.Mitchell);

    public ArtResizeResult? EnsureVersions(string cachePath, IReadOnlyList<int> widths)
    {
        ArgumentNullException.ThrowIfNull(widths);
        string original = store.FileFor(cachePath);
        if (!File.Exists(original))
        {
            return null;
        }

        int missingIfUnreadable = widths.Count(w => !File.Exists(store.VersionFor(cachePath, w)));
        try
        {
            using SKCodec codec = SKCodec.Create(original)
                ?? throw new ArtResizeException($"{cachePath} isn't an image that can be read.", missingIfUnreadable);
            int width = codec.Info.Width;
            int height = codec.Info.Height;
            IReadOnlyList<int> targets = ArtSizes.Targets(width, widths);
            int[] missing = targets
                .Where(w => !File.Exists(store.VersionFor(cachePath, w)))
                .ToArray();
            int made = missing.Length == 0 ? 0 : MakeVersions(codec, cachePath, width, height, missing);
            return new ArtResizeResult(new ArtVersions(width, targets), made);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            throw new ArtResizeException($"{cachePath}: {ex.Message}", missingIfUnreadable, ex);
        }
    }

    /// <summary>Decodes the original once, then writes each missing version.</summary>
    private int MakeVersions(SKCodec codec, string cachePath, int width, int height, int[] missing)
    {
        using SKBitmap source = SKBitmap.Decode(codec)
            ?? throw new ArtResizeException($"{cachePath} couldn't be decoded.", missing.Length);
        int made = 0;
        foreach (int target in missing)
        {
            // No colour space on the target: nothing to embed, so the WebP carries no ICC profile.
            SKImageInfo info = new(
                target,
                ArtSizes.ResizedHeight(width, height, target),
                SKColorType.Rgba8888,
                source.AlphaType == SKAlphaType.Opaque ? SKAlphaType.Opaque : SKAlphaType.Premul);
            using SKBitmap resized = source.Resize(info, Sampling)
                ?? throw new ArtResizeException(
                    $"{cachePath} couldn't be resized to {target} px.",
                    missing.Length - made);
            using SKData encoded = resized.Encode(SKEncodedImageFormat.Webp, ArtSizes.WebpQuality)
                ?? throw new ArtResizeException(
                    $"{cachePath} couldn't be encoded at {target} px.",
                    missing.Length - made);
            FileArtStore.WriteAtomically(store.VersionFor(cachePath, target), encoded.AsSpan());
            made++;
        }

        return made;
    }
}
