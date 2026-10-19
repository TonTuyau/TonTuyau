using TonTuyau.Application.Browse;
using TonTuyau.Application.Library;
using TonTuyau.Application.Paging;
using TonTuyau.Application.Store;
using TonTuyau.Application.Validation;
using TonTuyau.Application.YouTube;

namespace TonTuyau.Application.ChannelAdmin;

/// <summary>
/// A channel's admin page (1.5): the channel as TonTuyau knows it on YouTube, how many of its videos are in each
/// state, and its videos with theirs (latest published first, filtered by state or words). Refreshing them is a task
/// (<see cref="Tasks.TaskService"/>).
/// </summary>
public sealed class ChannelAdminService(IChannelRepository channels, IVideoRepository videos, CardBuilder cards)
{
    public const int MaxQueryLength = 100;

    /// <exception cref="NotFoundException">No channel with this id.</exception>
    public async Task<ChannelAdminView> GetAsync(string id, CancellationToken cancellationToken)
    {
        RequestRules.RequireChannelId(id);
        StoredChannel stored = await channels.GetStoredAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw NotFound(id);
        Channel channel = stored.Channel;
        IReadOnlyList<ChannelCardView> card = await cards
            .ChannelCardsAsync(
                [new ChannelListItem(
                    channel.Id,
                    channel.Name,
                    channel.Subscribers,
                    channel.ThumbPath,
                    stored.ThumbArt)],
                cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyDictionary<YouTubeState, long> counted = await videos
            .CountYouTubeStatesAsync(id, cancellationToken)
            .ConfigureAwait(false);
        return new ChannelAdminView(
            card[0],
            channel.YouTube,
            Enum.GetValues<YouTubeState>().ToDictionary(s => s, s => counted.GetValueOrDefault(s)));
    }

    /// <exception cref="ValidationException">The request isn't valid.</exception>
    /// <exception cref="NotFoundException">No channel with this id.</exception>
    public async Task<ResultPage<AdminVideoView>> ListVideosAsync(
        AdminVideosRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidationErrors errors = new();
        RequestRules.ChannelId(errors, "channelId", request.ChannelId);
        RequestRules.OneOf(errors, "state", request.State, ApiValues.YouTubeStates);
        RequestRules.Text(errors, "q", request.Q, required: false, MaxQueryLength);
        RequestRules.Size(errors, request.Size, ApiValues.MaxPageSize);
        errors.ThrowIfAny(RequestRules.Subject);
        IReadOnlyDictionary<string, ChannelListItem> found = await channels
            .GetListItemsAsync([request.ChannelId], cancellationToken)
            .ConfigureAwait(false);
        if (found.Count == 0)
        {
            throw NotFound(request.ChannelId);
        }

        VideoQuery query = new(
            string.IsNullOrWhiteSpace(request.Q) ? null : SearchText.Parse(request.Q),
            new VideoFilter(
                ChannelId: request.ChannelId,
                YouTube: request.State is null ? null : ApiValues.YouTubeStates[request.State]),
            VideoListSort.Latest);
        string fingerprint = Fingerprints.Of("admin-channel-videos", query);
        PageRequest page = new(
            request.Size ?? ApiValues.DefaultPageSize,
            CursorCodec.Decode(request.Cursor, fingerprint)?.After);
        StorePage<VideoListItem> listed = await videos.FindAsync(query, page, cancellationToken).ConfigureAwait(false);
        ResultPage<VideoCardView> shown = await cards
            .VideoPageAsync(listed, fingerprint, cancellationToken)
            .ConfigureAwait(false);
        return new ResultPage<AdminVideoView>(
            [
                .. shown.Items.Zip(
                    listed.Items,
                    (card, item) => new AdminVideoView(
                        card,
                        item.YouTube ?? YouTubeStatus.NotChecked)),
            ],
            shown.NextCursor,
            shown.Total);
    }

    private static NotFoundException NotFound(string id) => new($"There's no channel with the id '{id}'.");
}
