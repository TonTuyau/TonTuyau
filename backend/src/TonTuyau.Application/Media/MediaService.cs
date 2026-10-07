using System.Globalization;
using Microsoft.Extensions.Logging;
using TonTuyau.Application.Browse;
using TonTuyau.Application.Library;
using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;

namespace TonTuyau.Application.Media;

/// <param name="ContentType">What the file really is (see <see cref="MediaTypes.FileContentType"/>).</param>
public sealed record MediaFile(StoredFile File, string ContentType, DownloadName DownloadName);

/// <param name="ContentType">JPEG, PNG or WebP, as TubeArchivist made it.</param>
public sealed record ArtFile(StoredFile File, string ContentType);

/// <summary>
/// Finds the files the API serves. Always by id: id format → the stored video or channel → its indexed path →
/// checked by <see cref="LibraryPaths"/> → resolved under the media or art root. A path never comes from the URL.
/// </summary>
public sealed partial class MediaService(
    IVideoRepository videos,
    IChannelRepository channels,
    IMediaLibrary media,
    IArtStore art,
    ILogger<MediaService> logger)
{
    /// <exception cref="ValidationException">Not a video id.</exception>
    /// <exception cref="NotFoundException">Unknown video, or its file isn't on the share.</exception>
    public async Task<MediaFile> GetVideoFileAsync(string id, CancellationToken cancellationToken)
    {
        RequestRules.RequireVideoId(id);
        StoredVideo stored = await videos.GetStoredAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"There's no video with the id '{id}'.");
        Video video = stored.Video;
        NotFoundException unavailable = new($"The file of the video '{id}' isn't on the media share.");
        if (stored.MediaMissing)
        {
            throw unavailable;
        }

        if (!LibraryPaths.IsMediaPath(video.MediaPath))
        {
            // The sync validates paths before storing them: this is a damaged document, not a user mistake.
            LogBadPath(id, video.MediaPath);
            throw unavailable;
        }

        StoredFile file = media.Find(video.MediaPath) ?? throw unavailable;
        string extension = MediaTypes.ExtensionOf(video.MediaPath);
        return new MediaFile(
            file,
            MediaTypes.FileContentType(extension),
            DownloadFileName.For(video.Title, extension));
    }

    /// <exception cref="ValidationException">Not a video id.</exception>
    /// <exception cref="NotFoundException">Unknown video, or no copied thumbnail.</exception>
    public async Task<ArtFile> GetThumbnailAsync(string id, CancellationToken cancellationToken)
    {
        RequestRules.RequireVideoId(id);
        StoredVideo stored = await videos.GetStoredAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"There's no video with the id '{id}'.");
        return FindArt(stored.Video.ThumbnailPath, $"The video '{id}' has no thumbnail.");
    }

    /// <summary>A resized version of a video's thumbnail (WebP, contract v1.4).</summary>
    /// <param name="width">From the URL: only the widths the sync makes (360, 720); anything else is a 404.</param>
    /// <exception cref="ValidationException">Not a video id.</exception>
    /// <exception cref="NotFoundException">Unknown video, a width that isn't made, or a version not made (yet).</exception>
    public async Task<ArtFile> GetThumbnailVersionAsync(string id, string width, CancellationToken cancellationToken)
    {
        RequestRules.RequireVideoId(id);
        int size = SizeOf(width, ArtSizes.VideoThumbnail);
        StoredVideo stored = await videos.GetStoredAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"There's no video with the id '{id}'.");
        return FindVersion(stored.Video.ThumbnailPath, size, $"The video '{id}' has no thumbnail {size} px wide.");
    }

    /// <summary>A resized version of a channel's thumb or banner (WebP, contract v1.4).</summary>
    /// <param name="width">From the URL: thumb 48, 96, 176, 320; banner 1280; anything else is a 404.</param>
    /// <exception cref="ValidationException">Not a channel id.</exception>
    /// <exception cref="NotFoundException">Unknown channel or kind, a width that isn't made, or a version not made (yet).</exception>
    public async Task<ArtFile> GetChannelArtVersionAsync(
        string id,
        string kind,
        string width,
        CancellationToken cancellationToken)
    {
        RequestRules.RequireChannelId(id);
        if (!ApiValues.ArtKinds.TryGet(kind, out ChannelArtKind artKind))
        {
            throw new NotFoundException($"A channel has no image of this kind (only {ApiValues.ArtKinds.Choices}).");
        }

        int size = SizeOf(width, artKind == ChannelArtKind.Thumb ? ArtSizes.ChannelThumb : ArtSizes.ChannelBanner);
        Channel channel = await channels.GetAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"There's no channel with the id '{id}'.");
        string? path = artKind == ChannelArtKind.Thumb ? channel.ThumbPath : channel.BannerPath;
        string name = ApiValues.ArtKinds.NameOf(artKind);
        return FindVersion(path, size, $"The channel '{id}' has no {name} image {size} px wide.");
    }

    /// <param name="kind"><c>thumb</c> or <c>banner</c>. Any other kind (e.g. TubeArchivist's <c>tvart</c>, which the
    /// sync still copies) is an image that doesn't exist: a 404 (contract v1.1).</param>
    /// <exception cref="ValidationException">Not a channel id.</exception>
    /// <exception cref="NotFoundException">Unknown channel, unknown kind, or no copied image of this kind.</exception>
    public async Task<ArtFile> GetChannelArtAsync(string id, string kind, CancellationToken cancellationToken)
    {
        RequestRules.RequireChannelId(id);
        if (!ApiValues.ArtKinds.TryGet(kind, out ChannelArtKind artKind))
        {
            // The kind asked for isn't echoed back: it's whatever the URL held.
            throw new NotFoundException($"A channel has no image of this kind (only {ApiValues.ArtKinds.Choices}).");
        }

        return await GetChannelArtAsync(id, artKind, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ArtFile> GetChannelArtAsync(string id, ChannelArtKind kind, CancellationToken cancellationToken)
    {
        Channel channel = await channels.GetAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"There's no channel with the id '{id}'.");
        string? path = kind switch
        {
            ChannelArtKind.Thumb => channel.ThumbPath,
            ChannelArtKind.Banner => channel.BannerPath,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        return FindArt(path, $"The channel '{id}' has no {ApiValues.ArtKinds.NameOf(kind)} image.");
    }

    private ArtFile FindArt(string? cachePath, string missing)
    {
        if (cachePath is null)
        {
            throw new NotFoundException(missing);
        }

        if (!LibraryPaths.IsArtPath(cachePath))
        {
            LogBadArtPath(cachePath);
            throw new NotFoundException(missing);
        }

        StoredFile file = art.Find(cachePath) ?? throw new NotFoundException(missing);
        return new ArtFile(file, MediaTypes.ImageContentType(MediaTypes.ExtensionOf(cachePath)));
    }

    /// <summary>The width asked for, when it's one the sync makes; anything else (not a number too) is a 404.</summary>
    private static int SizeOf(string width, IReadOnlyList<int> made)
    {
        foreach (int size in made)
        {
            if (string.Equals(width, size.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
            {
                return size;
            }
        }

        // The width asked for isn't echoed back: it's whatever the URL held.
        throw new NotFoundException(
            $"No resized version of this width (only {string.Join(", ", made)}).");
    }

    private ArtFile FindVersion(string? cachePath, int width, string missing)
    {
        if (cachePath is null || !LibraryPaths.IsArtPath(cachePath))
        {
            throw new NotFoundException(missing);
        }

        StoredFile file = art.FindVersion(cachePath, width) ?? throw new NotFoundException(missing);
        return new ArtFile(file, MediaTypes.ImageContentType("webp"));
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Video {Id}: the stored media path '{Path}' isn't a media path.")]
    private partial void LogBadPath(string id, string path);

    [LoggerMessage(Level = LogLevel.Error, Message = "The stored art path '{Path}' isn't an art path.")]
    private partial void LogBadArtPath(string path);
}
