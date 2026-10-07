using TonTuyau.Application.Library;
using TonTuyau.Application.Paging;
using TonTuyau.Application.Store;
using TonTuyau.Application.Validation;

namespace TonTuyau.Application.Browse;

/// <summary>The channel list, a channel's page and its Videos / Shorts / Streams tabs.</summary>
public sealed class ChannelService(
    IChannelRepository channels,
    VideoService videos,
    CardBuilder cards,
    IValidator<ChannelListRequest> listValidator,
    IValidator<ChannelVideosRequest> videosValidator)
{
    public async Task<ResultPage<ChannelCardView>> ListAsync(
        ChannelListRequest request,
        CancellationToken cancellationToken)
    {
        listValidator.Validate(request);
        ChannelQuery query = new(
            string.IsNullOrWhiteSpace(request.Q) ? null : request.Q.Trim(),
            null,
            request.Sort is null ? ChannelListSort.Name : ApiValues.ChannelSorts[request.Sort]);
        string fingerprint = Fingerprints.Of("channels", query);
        PageRequest page = new(
            request.Size ?? ApiValues.DefaultPageSize,
            CursorCodec.Decode(request.Cursor, fingerprint)?.After);
        StorePage<ChannelListItem> found = await channels.FindAsync(query, page, cancellationToken)
            .ConfigureAwait(false);
        return await cards.ChannelPageAsync(found, fingerprint, cancellationToken).ConfigureAwait(false);
    }

    /// <exception cref="NotFoundException">No channel with this id.</exception>
    public async Task<ChannelDetailView> GetAsync(string id, CancellationToken cancellationToken)
    {
        RequestRules.RequireChannelId(id);
        StoredChannel stored = await channels.GetStoredAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw NotFound(id);
        Channel channel = stored.Channel;
        IReadOnlyList<ChannelCardView> card = await cards
            .ChannelCardsAsync(
                [
                    new ChannelListItem(
                        channel.Id,
                        channel.Name,
                        channel.Subscribers,
                        channel.ThumbPath,
                        stored.ThumbArt),
                ],
                cancellationToken)
            .ConfigureAwait(false);
        return new ChannelDetailView(
            card[0],
            channel.Description,
            cards.Image(channel.BannerPath, stored.BannerArt, ArtSizes.ChannelBanner),
            channel.Tags,
            channel.Active);
    }

    /// <summary>One tab of a channel: its videos of one type (default: videos), latest first by default.</summary>
    /// <exception cref="NotFoundException">No channel with this id.</exception>
    public async Task<ResultPage<VideoCardView>> ListVideosAsync(
        ChannelVideosRequest request,
        CancellationToken cancellationToken)
    {
        videosValidator.Validate(request);
        IReadOnlyDictionary<string, ChannelListItem> found = await channels
            .GetListItemsAsync([request.ChannelId], cancellationToken)
            .ConfigureAwait(false);
        if (found.Count == 0)
        {
            throw NotFound(request.ChannelId);
        }

        VideoQuery query = new(
            null,
            new VideoFilter(
                Type: VideoService.TypeOf(request.Type) ?? VideoType.Video,
                ChannelId: request.ChannelId,
                ExcludeId: request.Exclude),
            VideoService.SortOf(request.Sort));
        return await videos
            .ListAsync("channel-videos", query, request.Cursor, request.Size, cancellationToken)
            .ConfigureAwait(false);
    }

    private static NotFoundException NotFound(string id) => new($"There's no channel with the id '{id}'.");
}
