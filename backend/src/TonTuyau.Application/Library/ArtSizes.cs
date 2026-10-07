namespace TonTuyau.Application.Library;

/// <summary>Which images get resized versions.</summary>
public enum ArtSubject
{
    VideoThumbnail,
    ChannelThumb,
    ChannelBanner,

    /// <summary>TubeArchivist's TV art: copied by the sync, never served (contract v1.1), never resized.</summary>
    ChannelTvArt,
}

/// <summary>The image a cache path belongs to.</summary>
public sealed record ArtOwner(ArtSubject Subject, string Id);

/// <summary>
/// The resized versions the sync makes (QA09, contract v1.4): WebP, quality 80, these widths, never upscaled.
/// One table for the sync (what to make) and the API (which sized URLs exist).
/// </summary>
public static class ArtSizes
{
    public const int WebpQuality = 80;

    public static readonly IReadOnlyList<int> VideoThumbnail = [360, 720];

    public static readonly IReadOnlyList<int> ChannelThumb = [48, 96, 176, 320];

    public static readonly IReadOnlyList<int> ChannelBanner = [1280];

    /// <summary>The widths made for a subject (none for TV art).</summary>
    public static IReadOnlyList<int> For(ArtSubject subject) => subject switch
    {
        ArtSubject.VideoThumbnail => VideoThumbnail,
        ArtSubject.ChannelThumb => ChannelThumb,
        ArtSubject.ChannelBanner => ChannelBanner,
        ArtSubject.ChannelTvArt => [],
        _ => throw new ArgumentOutOfRangeException(nameof(subject)),
    };

    /// <summary>The widths worth making for an original this wide: strictly smaller ones only (never upscale).</summary>
    public static IReadOnlyList<int> Targets(int originalWidth, IReadOnlyList<int> widths)
    {
        ArgumentNullException.ThrowIfNull(widths);
        return widths
            .Where(w => w > 0 && w < originalWidth)
            .Distinct()
            .Order()
            .ToArray();
    }

    /// <summary>
    /// The height of a version <paramref name="targetWidth"/> wide, keeping the original's shape: rounded to the nearest
    /// pixel (halves away from zero), at least 1. Rounding (not truncating, not ceiling) keeps the sampler inside the
    /// source: a height one pixel too tall stretches the last source row into a blended edge line.
    /// </summary>
    public static int ResizedHeight(int originalWidth, int originalHeight, int targetWidth)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(originalWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(originalHeight);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetWidth);
        double height = originalHeight * (double)targetWidth / originalWidth;
        return Math.Max(1, (int)Math.Round(height, MidpointRounding.AwayFromZero));
    }
}
