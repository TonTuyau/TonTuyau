using TonTuyau.Application.Paging;
using TonTuyau.Application.Store;
using TonTuyau.Application.Validation;

namespace TonTuyau.Application.Browse;

/// <summary>The search page and the search box's suggestions.</summary>
public sealed class SearchService(
    IVideoRepository videos,
    IChannelRepository channels,
    CardBuilder cards,
    TimeProvider clock,
    IValidator<VideoSearchRequest> searchValidator,
    IValidator<SuggestRequest> suggestValidator)
{
    public async Task<SearchResultsView> SearchAsync(VideoSearchRequest request, CancellationToken cancellationToken)
    {
        searchValidator.Validate(request);
        // Not trimmed: a trailing space says the last word is complete (QA10 M5-1).
        SearchText text = SearchText.Parse(request.Q!);
        SearchKind kind = request.Kind is null ? SearchKind.All : ApiValues.Kinds[request.Kind];
        string scope = "search-" + ApiValues.Kinds.NameOf(kind);
        int size = request.Size ?? ApiValues.DefaultPageSize;
        ChannelQuery channelQuery = new(null, text, ChannelListSort.Relevance);

        if (kind == SearchKind.Channels)
        {
            string fingerprint = Fingerprints.Of(scope, channelQuery);
            SortPosition? after = CursorCodec.Decode(request.Cursor, fingerprint)?.After;
            StorePage<ChannelListItem> found = await channels
                .FindAsync(
                    channelQuery,
                    new PageRequest(size, after),
                    cancellationToken)
                .ConfigureAwait(false);
            ResultPage<ChannelCardView> page = await cards.ChannelPageAsync(found, fingerprint, cancellationToken)
                .ConfigureAwait(false);
            return new SearchResultsView(kind, null, null, page);
        }

        PublishedPeriod? period = request.Published is null ? null : ApiValues.Periods[request.Published];
        VideoQuery videoQuery = new(text, FilterOf(request), SortOf(request.Sort));
        string videoFingerprint = Fingerprints.Of(scope, videoQuery, period);
        PageCursor? cursor = CursorCodec.Decode(request.Cursor, videoFingerprint);
        DateTimeOffset? since = SinceOf(period, cursor);
        PageRequest videoPage = new(size, cursor?.After);
        StorePage<VideoListItem> foundVideos = await videos
            .FindAsync(
                videoQuery with { Filter = videoQuery.Filter with { PublishedFrom = since } },
                videoPage,
                cancellationToken)
            .ConfigureAwait(false);
        ResultPage<VideoCardView> videoResults = await cards
            .VideoPageAsync(foundVideos, videoFingerprint, cancellationToken, since)
            .ConfigureAwait(false);

        // kind=all: the best channels above the videos, on the first page only.
        IReadOnlyList<ChannelCardView>? top = null;
        if (kind == SearchKind.All)
        {
            top = [];
            if (videoPage.After is null)
            {
                StorePage<ChannelListItem> best = await channels
                    .FindAsync(
                        channelQuery,
                        new PageRequest(ApiValues.TopChannels, null),
                        cancellationToken)
                    .ConfigureAwait(false);
                top = await cards.ChannelCardsAsync(best.Items, cancellationToken).ConfigureAwait(false);
            }
        }

        return new SearchResultsView(kind, top, videoResults, null);
    }

    /// <summary>
    /// As-you-type: up to <see cref="ApiValues.SuggestedChannels"/> channels whose name starts with what was typed,
    /// then video titles fill the rest.
    /// </summary>
    public async Task<IReadOnlyList<SuggestionView>> SuggestAsync(
        SuggestRequest request,
        CancellationToken cancellationToken)
    {
        suggestValidator.Validate(request);
        string prefix = request.Q!.Trim();
        int size = request.Size ?? ApiValues.DefaultSuggestSize;
        int channelCount = Math.Min(size, ApiValues.SuggestedChannels);
        IReadOnlyList<TextMatch> names = await channels
            .SuggestNamesAsync(prefix, channelCount, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<TextMatch> titles = names.Count < size
            ? await videos.SuggestTitlesAsync(prefix, size - names.Count, cancellationToken).ConfigureAwait(false)
            : [];
        return
        [
            .. names.Select(n => new SuggestionView(n.Text, SuggestionKind.Channel, n.Id)),
            .. titles.Select(t => new SuggestionView(t.Text, SuggestionKind.Video, t.Id)),
        ];
    }

    /// <summary>
    /// The <c>published</c> cut-off: now minus the period on the first page, then the one frozen in the cursor, so
    /// a page turned later (or a day later) keeps exactly the same list.
    /// </summary>
    /// <exception cref="ValidationException">A cursor of this search without its cut-off (hand-made).</exception>
    private DateTimeOffset? SinceOf(PublishedPeriod? period, PageCursor? cursor)
    {
        if (period is null)
        {
            return cursor?.Since is null ? null : throw CursorCodec.Invalid();
        }

        if (cursor is null)
        {
            return clock.GetUtcNow() - ApiValues.LengthOf(period.Value);
        }

        return cursor.Since ?? throw CursorCodec.Invalid();
    }

    /// <summary>The filters as asked, before the <c>published</c> cut-off is known (see <see cref="SinceOf"/>).</summary>
    private static VideoFilter FilterOf(VideoSearchRequest request) => new(
        VideoService.TypeOf(request.Type),
        request.Channel,
        null,
        request.Duration is null ? null : ApiValues.Durations[request.Duration]);

    private static VideoListSort SortOf(string? sort) =>
        sort is null ? VideoListSort.Relevance : ApiValues.SearchSorts[sort];
}
