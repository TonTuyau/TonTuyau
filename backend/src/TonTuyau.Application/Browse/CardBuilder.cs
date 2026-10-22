using TonTuyau.Application.Library;
using TonTuyau.Application.Paging;
using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;
using TonTuyau.Application.YouTube;

namespace TonTuyau.Application.Browse;

/// <summary>
/// Turns store items into cards: adds what lives elsewhere (the channel's thumbnail for a video, the per-type video
/// counts for a channel) and each image's versions, as the import or a refresh recorded them in the store (QA09: no
/// disk access per card). An image not recorded yet (a store from before resized art) is looked up in the art store.
/// </summary>
public sealed class CardBuilder(IVideoRepository videos, IChannelRepository channels, IArtStore art)
{
    public async Task<IReadOnlyList<VideoCardView>> VideoCardsAsync(
        IReadOnlyList<VideoListItem> items,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        string[] channelIds = items
            .Select(i => i.ChannelId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        IReadOnlyDictionary<string, ChannelListItem> owners = channelIds.Length == 0
            ? new Dictionary<string, ChannelListItem>()
            : await channels.GetListItemsAsync(channelIds, cancellationToken).ConfigureAwait(false);
        return items
            .Select(i => VideoCard(i, owners.GetValueOrDefault(i.ChannelId)))
            .ToArray();
    }

    public async Task<IReadOnlyList<ChannelCardView>> ChannelCardsAsync(
        IReadOnlyList<ChannelListItem> items,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
        {
            return [];
        }

        IReadOnlyDictionary<string, TypeCounts> counts = await videos
            .CountByTypeAsync(
                [.. items.Select(i => i.Id)],
                cancellationToken)
            .ConfigureAwait(false);
        return items
            .Select(i => new ChannelCardView(
                i.Id,
                i.Name,
                i.Subscribers,
                Image(i.ThumbPath, i.ThumbArt, ArtSizes.ChannelThumb),
                counts.GetValueOrDefault(i.Id, TypeCounts.None)))
            .ToArray();
    }

    /// <param name="since">The search's frozen <c>published</c> cut-off, carried to the next page in the cursor.</param>
    public async Task<ResultPage<VideoCardView>> VideoPageAsync(
        StorePage<VideoListItem> page,
        string fingerprint,
        CancellationToken cancellationToken,
        DateTimeOffset? since = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        IReadOnlyList<VideoCardView> cards = await VideoCardsAsync(page.Items, cancellationToken).ConfigureAwait(false);
        return new ResultPage<VideoCardView>(
            cards,
            NextCursor(page, fingerprint, since),
            page.Total);
    }

    public async Task<ResultPage<ChannelCardView>> ChannelPageAsync(
        StorePage<ChannelListItem> page,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        IReadOnlyList<ChannelCardView> cards = await ChannelCardsAsync(page.Items, cancellationToken)
            .ConfigureAwait(false);
        return new ResultPage<ChannelCardView>(
            cards,
            NextCursor(page, fingerprint),
            page.Total);
    }

    /// <summary>
    /// The image's versions: as recorded in the store, or, when they aren't, what the art store holds (null
    /// when the path isn't an art path or the image was never copied).
    /// </summary>
    public ArtVersions? Image(string? cachePath, ArtVersions? recorded, IReadOnlyList<int> widths)
    {
        if (!LibraryPaths.IsArtPath(cachePath))
        {
            return null;
        }

        return recorded ?? art.Describe(cachePath!, widths);
    }

    public static VideoListItem ListItemOf(Video video, ArtVersions? thumbnailArt = null)
    {
        ArgumentNullException.ThrowIfNull(video);
        return new VideoListItem(
            video.Id,
            video.ChannelId,
            video.ChannelName,
            video.Title,
            video.Type,
            !video.GoneFromYouTube,
            video.Published,
            video.Duration,
            video.Stats.Views,
            video.ThumbnailPath,
            thumbnailArt,
            video.YouTube);
    }

    private VideoCardView VideoCard(VideoListItem item, ChannelListItem? owner) => new(
        item.Id,
        item.Title,
        item.Type,
        item.Published,
        item.Duration,
        item.Views,
        item.Active,
        item.YouTube?.State ?? YouTubeState.NotChecked,
        Image(item.ThumbnailPath, item.ThumbnailArt, ArtSizes.VideoThumbnail),
        new ChannelRefView(
            item.ChannelId,
            item.ChannelName,
            owner is null ? null : Image(owner.ThumbPath, owner.ThumbArt, ArtSizes.ChannelThumb),
            owner?.Subscribers ?? 0));

    private static string? NextCursor<T>(StorePage<T> page, string fingerprint, DateTimeOffset? since = null) =>
        page.Next is null ? null : CursorCodec.Encode(fingerprint, page.Next, since);
}
