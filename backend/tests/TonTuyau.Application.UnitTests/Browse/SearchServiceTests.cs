using Microsoft.Extensions.Time.Testing;
using TonTuyau.Application.Browse;
using TonTuyau.Application.Library;
using TonTuyau.Application.Paging;
using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;
using TonTuyau.Application.Validation;
using TonTuyau.Application.YouTube;

namespace TonTuyau.Application.UnitTests.Browse;

/// <summary>The search use cases on recording fakes and a frozen clock (no Elasticsearch).</summary>
public sealed class SearchServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _clock = new(Now);
    private readonly RecordingVideos _videos = new();
    private readonly RecordingChannels _channels = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private SearchService Service => new(
        _videos,
        _channels,
        new CardBuilder(_videos, _channels, new NoArt()),
        _clock,
        new VideoSearchRequestValidator(),
        new SuggestRequestValidator());

    [Theory]
    [InlineData("today", 1)]
    [InlineData("week", 7)]
    [InlineData("month", 30)]
    [InlineData("year", 365)]
    public async Task The_published_cut_off_is_now_minus_the_period(string published, int days)
    {
        await Service.SearchAsync(Search(published), Ct);

        Assert.Equal(Now.AddDays(-days), Assert.Single(_videos.Queries).Filter.PublishedFrom);
    }

    [Fact]
    public async Task Without_a_period_there_is_no_cut_off()
    {
        await Service.SearchAsync(Search(null), Ct);

        Assert.Null(Assert.Single(_videos.Queries).Filter.PublishedFrom);
    }

    [Theory, CombinatorialData]
    public async Task Later_pages_keep_the_cut_off_of_the_first_even_when_time_passed(
        [CombinatorialValues("today", "week", "month", "year")] string published,
        [CombinatorialValues(1, 40, 400)] int daysLater)
    {
        _videos.HasMore = true;
        SearchResultsView first = await Service.SearchAsync(Search(published), Ct);
        _clock.Advance(TimeSpan.FromDays(daysLater));

        VideoSearchRequest next = Search(published, first.Videos!.NextCursor);
        await Service.SearchAsync(next, Ct);

        Assert.Equal(2, _videos.Queries.Count);
        Assert.Equal(_videos.Queries[0].Filter.PublishedFrom, _videos.Queries[1].Filter.PublishedFrom);
    }

    [Theory]
    [InlineData(null, "week")]
    [InlineData("week", null)]
    [InlineData("today", "week")]
    [InlineData("month", "year")]
    public async Task A_cursor_of_another_period_is_refused(string? first, string? then)
    {
        _videos.HasMore = true;
        SearchResultsView page = await Service.SearchAsync(Search(first), Ct);

        VideoSearchRequest next = Search(then, page.Videos!.NextCursor);

        ValidationException error = await Assert.ThrowsAsync<ValidationException>(
            () => Service.SearchAsync(next, Ct));

        Assert.Equal("cursor", Assert.Single(error.Errors).Field);
    }

    [Fact]
    public async Task Later_pages_of_kind_all_have_an_empty_channel_list()
    {
        _videos.HasMore = true;
        SearchResultsView first = await Service.SearchAsync(Search(null), Ct);
        int channelSearches = _channels.Searches;

        VideoSearchRequest next = Search(null, first.Videos!.NextCursor);
        SearchResultsView second = await Service.SearchAsync(next, Ct);

        Assert.NotNull(second.TopChannels);
        Assert.Empty(second.TopChannels);
        Assert.Equal(channelSearches, _channels.Searches);
    }

    [Theory, CombinatorialData]
    public async Task At_most_three_channels_open_the_suggestions_and_titles_fill_the_rest(
        [CombinatorialValues(1, 2, 3, 4, 8, 20)] int size,
        [CombinatorialValues(0, 1, 3, 10)] int channelsFound)
    {
        _channels.NamesFound = channelsFound;

        SuggestRequest request = new("c", size);
        IReadOnlyList<SuggestionView> suggestions = await Service.SuggestAsync(request, Ct);

        int asked = Math.Min(size, 3);
        int channels = Math.Min(asked, channelsFound);
        SuggestionKind[] expected =
        [
            .. Enumerable.Repeat(SuggestionKind.Channel, channels),
            .. Enumerable.Repeat(SuggestionKind.Video, size - channels),
        ];
        Assert.Equal(asked, _channels.AskedNames);
        Assert.Equal(expected, suggestions.Select(s => s.Kind));
    }

    private static VideoSearchRequest Search(string? published, string? cursor = null) =>
        new("creme", null, null, null, published, null, null, cursor, 1);

    private sealed class NoArt : IArtStore
    {
        public bool Exists(string cachePath) => false;

        public Task SaveAsync(string cachePath, Stream content, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public void Delete(string cachePath) => throw new NotSupportedException();

        public StoredFile? Find(string cachePath) => null;

        public StoredFile? FindVersion(string cachePath, int width) => null;

        public ArtVersions? Describe(string cachePath, IReadOnlyList<int> widths) => null;
    }

    /// <summary>Records each list query; answers one video, with a next page when <see cref="HasMore"/>.</summary>
    private sealed class RecordingVideos : IVideoRepository
    {
        public List<VideoQuery> Queries { get; } = [];

        public bool HasMore { get; set; }

        public Task<StorePage<VideoListItem>> FindAsync(
            VideoQuery query,
            PageRequest page,
            CancellationToken cancellationToken)
        {
            Queries.Add(query);
            VideoListItem item = new(
                "abcdefghijk",
                "UCaaaaaaaaaaaaaaaaaaaaaa",
                "Chaîne",
                "Crème",
                VideoType.Video,
                true,
                Now,
                TimeSpan.FromMinutes(1),
                1,
                "/cache/videos/a/abcdefghijk.jpg");
            SortPosition next = new([new RealSortValue(1.5), new TextSortValue(item.Id)]);
            return Task.FromResult(new StorePage<VideoListItem>([item], HasMore ? next : null, 2));
        }

        public Task<IReadOnlyList<TextMatch>> SuggestTitlesAsync(
            string prefix,
            int size,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TextMatch>>(
                [.. Enumerable
                    .Range(0, size)
                    .Select(i => new TextMatch($"video{i:00000}", $"Titre {i}"))]);

        public Task<IReadOnlyDictionary<string, TypeCounts>> CountByTypeAsync(
            IReadOnlyCollection<string> channelIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, TypeCounts>>(new Dictionary<string, TypeCounts>());

        public Task<IReadOnlyDictionary<YouTubeState, long>> CountYouTubeStatesAsync(
            string channelId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StoredVideo?> GetStoredAsync(string id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<UpsertResult> UpsertAsync(
            IReadOnlyCollection<Video> videos,
            string? runId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Video?> GetAsync(string id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<long> CountAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<int> DeleteAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task MarkSeenAsync(IReadOnlyCollection<string> ids, string runId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<VideoSummary>> FindNotSeenAsync(
            string runId,
            IReadOnlyCollection<string> channelIds,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<VideoSummary>> FindByChannelsAsync(
            IReadOnlyCollection<string> channelIds,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlySet<string>> GetMediaMissingIdsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SetThumbnailArtAsync(
            IReadOnlyDictionary<string, ArtVersions> art,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SetMediaMissingAsync(
            IReadOnlyCollection<string> ids,
            bool missing,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    /// <summary>Counts channel searches; suggests <see cref="NamesFound"/> names at most.</summary>
    private sealed class RecordingChannels : IChannelRepository
    {
        public int Searches { get; private set; }

        public int NamesFound { get; set; }

        public int AskedNames { get; private set; }

        public Task<StorePage<ChannelListItem>> FindAsync(
            ChannelQuery query,
            PageRequest page,
            CancellationToken cancellationToken)
        {
            Searches++;
            return Task.FromResult(new StorePage<ChannelListItem>([], null, 0));
        }

        public Task<IReadOnlyList<TextMatch>> SuggestNamesAsync(
            string prefix,
            int size,
            CancellationToken cancellationToken)
        {
            AskedNames = size;
            int found = Math.Min(size, NamesFound);
            return Task.FromResult<IReadOnlyList<TextMatch>>(
                [.. Enumerable
                    .Range(0, found)
                    .Select(i => new TextMatch($"UC{i:0000000000000000000000}", $"Chaîne {i}"))]);
        }

        public Task<IReadOnlyDictionary<string, ChannelListItem>> GetListItemsAsync(
            IReadOnlyCollection<string> ids,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, ChannelListItem>>(new Dictionary<string, ChannelListItem>());

        public Task<UpsertResult> UpsertAsync(
            IReadOnlyCollection<Channel> channels,
            string? runId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Channel?> GetAsync(string id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<long> CountAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<int> DeleteAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task MarkSeenAsync(IReadOnlyCollection<string> ids, string runId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StoredChannel?> GetStoredAsync(string id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SetArtAsync(
            ArtSubject subject,
            IReadOnlyDictionary<string, ArtVersions> art,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ChannelSummary>> FindNotSeenAsync(
            string runId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
