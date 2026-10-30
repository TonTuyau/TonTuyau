using System.Globalization;
using TonTuyau.Application.Browse;
using TonTuyau.Application.Dashboard;
using TonTuyau.Application.Library;
using TonTuyau.Application.Paging;
using TonTuyau.Application.Store;
using TonTuyau.Application.Validation;
using TonTuyau.Application.YouTube;

namespace TonTuyau.Application.ChannelAdmin;

/// <summary>
/// A channel's admin page (1.5): the channel as TonTuyau knows it on YouTube, how many of its videos are in each
/// state, and its videos with theirs (latest published first, filtered by state or words). Refreshing them is a task
/// (<see cref="Tasks.TaskService"/>). Its subscription (2.0, B4) is set here: subscribed, its tabs, unsubscribed. Its
/// files' sizes by type (2.2.0) are counted as the dashboard counts them. Its videos whose files were purged on their
/// own (2.3.0) are listed apart, their syncs blocked, and unblocked here.
/// </summary>
public sealed class ChannelAdminService(
    IChannelRepository channels,
    IVideoRepository videos,
    IDashboardRepository stats,
    ISubscriptionStore subscriptions,
    IDownloadFailureStore downloadFailures,
    CardBuilder cards,
    TimeProvider clock)
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
        ChannelSubscription? subscription = await subscriptions
            .GetAsync(id, cancellationToken)
            .ConfigureAwait(false);
        LibraryStats library = await stats.GetLibraryStatsAsync([id], cancellationToken).ConfigureAwait(false);

        // 2.3.0: the index leaves out the videos whose syncs are blocked; the admin page's list shows them.
        IReadOnlyList<StoredVideo> blocked = await videos
            .ListSyncBlockedAsync(id, cancellationToken)
            .ConfigureAwait(false);
        return new ChannelAdminView(
            card[0],
            channel.YouTube,
            Enum.GetValues<YouTubeState>()
                .ToDictionary(
                    s => s,
                    s => counted.GetValueOrDefault(s)
                        + blocked.Count(b => (b.Video.YouTube?.State ?? YouTubeState.NotChecked) == s)),
            subscription,
            library.Channels.GetValueOrDefault(id)?.Sizes ?? TypeCounts.None)
        {
            SyncBlocked = blocked.Count,
        };
    }

    /// <summary>
    /// Subscribes to the channel (2.0, B4), or changes the tabs of its subscription: when, by whom and where from it
    /// was subscribed then stay.
    /// </summary>
    /// <param name="account">Who subscribes it.</param>
    /// <exception cref="ValidationException">A tab missing, or none of them ticked.</exception>
    /// <exception cref="NotFoundException">No channel with this id.</exception>
    public async Task<ChannelSubscription> SubscribeAsync(
        string id,
        bool? videos,
        bool? shorts,
        bool? streams,
        string account,
        CancellationToken cancellationToken)
    {
        RequestRules.RequireChannelId(id);
        ValidationErrors errors = new();
        foreach ((string field, bool? value) in new[] { ("videos", videos), ("shorts", shorts), ("streams", streams) })
        {
            if (value is null)
            {
                errors.Add(field, "is required: true or false.");
            }
        }

        if (videos == false && shorts == false && streams == false)
        {
            errors.Add("videos", "at least one tab: unsubscribe instead.");
        }

        errors.ThrowIfAny(RequestRules.Subject);
        if (await channels.GetAsync(id, cancellationToken).ConfigureAwait(false) is null)
        {
            throw NotFound(id);
        }

        ChannelSubscription wanted = new(
            id,
            new ChannelTabs(videos!.Value, shorts!.Value, streams!.Value),
            clock.GetUtcNow(),
            account,
            LibraryOrigin.TonTuyau);
        return await subscriptions
            .SetAsync(wanted, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// The videos of the channel the YouTube syncs couldn't download (2.0, B5), the latest failed first: why, how many
    /// times, and whether the incremental sync leaves them now.
    /// </summary>
    /// <exception cref="NotFoundException">No channel with this id.</exception>
    public async Task<IReadOnlyList<DownloadFailure>> ListFailedDownloadsAsync(
        string id,
        CancellationToken cancellationToken)
    {
        RequestRules.RequireChannelId(id);
        if (await channels.GetAsync(id, cancellationToken).ConfigureAwait(false) is null)
        {
            throw NotFound(id);
        }

        return await downloadFailures
            .ListAsync(id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// The channel's videos whose files were purged on their own (2.3.0), the latest published first: no sync downloads
    /// them, the library's lists leave them out.
    /// </summary>
    /// <exception cref="NotFoundException">No channel with this id.</exception>
    public async Task<IReadOnlyList<AdminVideoView>> ListSyncBlockedAsync(
        string id,
        CancellationToken cancellationToken)
    {
        RequestRules.RequireChannelId(id);
        if (await channels.GetAsync(id, cancellationToken).ConfigureAwait(false) is null)
        {
            throw NotFound(id);
        }

        IReadOnlyList<StoredVideo> blocked = await videos
            .ListSyncBlockedAsync(id, cancellationToken)
            .ConfigureAwait(false);
        return await BlockedViewsAsync(blocked, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Blocked videos as the channel's admin page shows them.</summary>
    private async Task<IReadOnlyList<AdminVideoView>> BlockedViewsAsync(
        IReadOnlyList<StoredVideo> blocked,
        CancellationToken cancellationToken)
    {
        VideoListItem[] items = [.. blocked.Select(s => CardBuilder.ListItemOf(s.Video, s.ThumbnailArt))];
        IReadOnlyList<VideoCardView> made = await cards
            .VideoCardsAsync(items, cancellationToken)
            .ConfigureAwait(false);
        return
        [
            .. made.Zip(
                blocked,
                (card, stored) => new AdminVideoView(card, stored.Video.YouTube, SyncBlocked: true)),
        ];
    }

    /// <summary>
    /// Unblocks the syncs of some of the channel's videos, or all of them (2.3.0): back in the library's lists, without
    /// their files; "Download again", or the full sync, downloads them.
    /// </summary>
    /// <param name="videoIds">Its videos to unblock, by commas; null: every one of them.</param>
    /// <returns>How many were unblocked (a video of another channel, or not blocked, is left).</returns>
    /// <exception cref="ValidationException">Not video ids.</exception>
    /// <exception cref="NotFoundException">No channel with this id.</exception>
    public async Task<int> UnblockSyncAsync(string id, string? videoIds, CancellationToken cancellationToken)
    {
        RequestRules.RequireChannelId(id);
        string[]? asked = videoIds?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (asked is not null && (asked.Length == 0 || !asked.All(LibraryIds.IsVideoId)))
        {
            throw RequestRules.Invalid("videos", "must be YouTube video ids by commas; none for all of them.");
        }

        if (await channels.GetAsync(id, cancellationToken).ConfigureAwait(false) is null)
        {
            throw NotFound(id);
        }

        IReadOnlyList<StoredVideo> blocked = await videos
            .ListSyncBlockedAsync(id, cancellationToken)
            .ConfigureAwait(false);
        string[] unblocked =
        [
            .. blocked.Select(s => s.Video.Id).Where(v => asked is null || asked.Contains(v, StringComparer.Ordinal)),
        ];
        return unblocked.Length == 0
            ? 0
            : await videos.SetSyncBlockedAsync(unblocked, blocked: false, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Unsubscribes from the channel (2.0, B4): it and its videos stay; nothing is downloaded any more.</summary>
    /// <exception cref="NotFoundException">No channel with this id.</exception>
    public async Task UnsubscribeAsync(string id, CancellationToken cancellationToken)
    {
        RequestRules.RequireChannelId(id);
        if (await channels.GetAsync(id, cancellationToken).ConfigureAwait(false) is null)
        {
            throw NotFound(id);
        }

        await subscriptions
            .RemoveAsync(id, cancellationToken)
            .ConfigureAwait(false);
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

        // 2.3.0: the videos whose syncs are blocked aren't in the index (the library's lists leave them out); the first
        // page shows them on top, those of the state asked for whose titles have the words typed. Each page counts
        // them in its total.
        bool Asked(StoredVideo stored) =>
            (query.Filter.YouTube is null
                || (stored.Video.YouTube?.State ?? YouTubeState.NotChecked) == query.Filter.YouTube)
            && (query.Text is null || HasWords(stored.Video.Title, query.Text));
        StoredVideo[] asked =
        [
            .. (await videos.ListSyncBlockedAsync(request.ChannelId, cancellationToken).ConfigureAwait(false))
                .Where(Asked),
        ];
        IReadOnlyList<AdminVideoView> blocked = request.Cursor is not null
            ? []
            : await BlockedViewsAsync(asked, cancellationToken).ConfigureAwait(false);
        if (request.Blocked == true)
        {
            return new ResultPage<AdminVideoView>(blocked, null, asked.Length);
        }

        StorePage<VideoListItem> listed = await videos.FindAsync(query, page, cancellationToken).ConfigureAwait(false);
        ResultPage<VideoCardView> shown = await cards
            .VideoPageAsync(listed, fingerprint, cancellationToken)
            .ConfigureAwait(false);
        return new ResultPage<AdminVideoView>(
            [
                .. blocked,
                .. shown.Items.Zip(
                    listed.Items,
                    (card, item) => new AdminVideoView(
                        card,
                        item.YouTube ?? YouTubeStatus.NotChecked)),
            ],
            shown.NextCursor,
            shown.Total + asked.Length);
    }

    /// <summary>
    /// Every word typed is in the title, ignoring case and accents as the index does ("creme" finds "Crème"): a blocked
    /// video isn't searched in the index.
    /// </summary>
    private static bool HasWords(string title, SearchText text) =>
        text.AllWords
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .All(word => CultureInfo.InvariantCulture.CompareInfo.IndexOf(
                title,
                word,
                CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0);

    private static NotFoundException NotFound(string id) => new($"There's no channel with the id '{id}'.");
}
