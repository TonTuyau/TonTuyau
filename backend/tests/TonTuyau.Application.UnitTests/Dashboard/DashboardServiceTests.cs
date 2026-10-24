using Microsoft.Extensions.Time.Testing;
using TonTuyau.Application.Dashboard;
using TonTuyau.Application.Library;
using TonTuyau.Application.Paging;
using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;
using TonTuyau.Application.UnitTests.Configuration;
using TonTuyau.Application.Validation;

namespace TonTuyau.Application.UnitTests.Dashboard;

/// <summary>What the dashboard computes from the store's counts: <see cref="IDashboardRepository"/> is a fake.</summary>
public sealed class DashboardServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeStore _store = new();
    private readonly FakeDiskSpace _disk = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_totals_are_the_store_counts()
    {
        _store.Channels = [Channel("a", "Alpha"), Channel("b", "Beta"), Channel("c", "Gamma")];
        _store.Stats = new LibraryStats(
            [Type(VideoType.Video, 120, hours: 30), Type(VideoType.Short, 40, hours: 0.5)],
            7,
            new Dictionary<string, ChannelStats>());

        LibraryTotalsView totals = (await Service().GetAsync(Ct)).Totals;

        Assert.Equal(new LibraryTotalsView(3, 120, 40, 0, 7), totals);
        Assert.Empty(_store.CountedChannels);
    }

    [Fact]
    public async Task The_play_time_adds_up_the_types_with_each_share_and_average_length()
    {
        _store.Stats = new LibraryStats(
            [
                Type(VideoType.Stream, 4, hours: 10),
                Type(VideoType.Video, 60, hours: 30),
            ],
            0,
            new Dictionary<string, ChannelStats>());

        PlayTimeView playTime = (await Service().GetAsync(Ct)).PlayTime;

        Assert.Equal(TimeSpan.FromHours(40), playTime.Total);
        Assert.Equal(
            [
                new TypePlayTimeView(VideoType.Video, 60, TimeSpan.FromHours(30), 0.75, TimeSpan.FromMinutes(30)),
                new TypePlayTimeView(VideoType.Short, 0, TimeSpan.Zero, 0, TimeSpan.Zero),
                new TypePlayTimeView(VideoType.Stream, 4, TimeSpan.FromHours(10), 0.25, TimeSpan.FromHours(2.5)),
            ],
            playTime.Types);
    }

    [Fact]
    public async Task An_empty_library_has_no_play_time_and_no_share()
    {
        PlayTimeView playTime = (await Service().GetAsync(Ct)).PlayTime;

        Assert.Equal(TimeSpan.Zero, playTime.Total);
        Assert.All(playTime.Types, t => Assert.Equal((0L, 0.0, TimeSpan.Zero), (t.Count, t.Share, t.Average)));
    }

    [Fact]
    public async Task The_storage_is_the_disk_and_the_videos_sizes_by_type_every_type_with_the_threshold()
    {
        _disk.Space = new DiskSpace(1_000_000_000_000, 412_000_000_000);
        _store.Stats = new LibraryStats(
            [
                new TypeStats(VideoType.Stream, 4, TimeSpan.FromHours(10), 238_000_000_000),
                Type(VideoType.Video, 60, 30),
            ],
            0,
            new Dictionary<string, ChannelStats>());

        StorageView storage = (await Service().GetAsync(Ct)).Storage;

        Assert.Equal(new DiskSpace(1_000_000_000_000, 412_000_000_000), storage.Disk);
        Assert.Equal(
            [
                new TypeStorageView(VideoType.Video, 0, 60),
                new TypeStorageView(VideoType.Short, 0, 0),
                new TypeStorageView(VideoType.Stream, 238_000_000_000, 4),
            ],
            storage.Types);
        // The defaults: a warning under 50 GB, the downloads held then.
        Assert.Equal((50_000_000_000L, true), (storage.MinFree, storage.HoldDownloads));
    }

    [Fact]
    public async Task A_disk_that_doesnt_say_is_no_disk()
    {
        Assert.Null((await Service().GetAsync(Ct)).Storage.Disk);
    }

    [Fact]
    public async Task The_channels_sort_by_their_files_sizes_together()
    {
        GivenThreeChannels();
        _store.Stats = _store.Stats with
        {
            Channels = new Dictionary<string, ChannelStats>
            {
                ["a"] = new(new TypeCounts(3, 1, 0), 1, 0, 0, 2, 0, new TypeCounts(3_000, 40, 0)),
                ["c"] = new(new TypeCounts(10, 0, 2), 4, 0, 0, 0, 5, new TypeCounts(1_000, 0, 500)),
            },
        };

        ResultPage<ChannelTotalsView> page = await Service().ListChannelsAsync(
            new DashboardChannelsRequest(null, "size", null, null, null, null),
            Ct);

        Assert.Equal([("a", 3_040L), ("c", 1_500L), ("b", 0L)], page.Items.Select(c => (c.Id, c.Size)));
    }

    [Fact]
    public async Task Every_channel_is_listed_biggest_first_even_without_videos()
    {
        GivenThreeChannels();

        ResultPage<ChannelTotalsView> page = await Service().ListChannelsAsync(Request(), Ct);

        Assert.Equal(
            [("c", 12L, 4L, 0L, 5L), ("a", 4L, 1L, 2L, 0L), ("b", 0L, 0L, 0L, 0L)],
            page.Items.Select(c => (c.Id, c.Total, c.Gone, c.RetryLater, c.NotChecked)));
        Assert.Equal(new TypeCounts(10, 0, 2), page.Items[0].Counts);
        Assert.Equal(TypeCounts.None, page.Items[2].Counts);
        Assert.Equal(3, page.Total);
        Assert.Null(page.NextCursor);
        Assert.Equal(["a", "b", "c"], _store.CountedChannels);
    }

    [Theory]
    [InlineData("name", null, "a, b, c")]
    [InlineData("name", "desc", "c, b, a")]
    [InlineData("gone", "asc", "b, a, c")]
    [InlineData("retryLater", null, "a, b, c")]
    [InlineData("total", "asc", "b, a, c")]
    public async Task The_channels_are_sorted_by_a_column_then_by_name(string sort, string? order, string ids)
    {
        GivenThreeChannels();

        ResultPage<ChannelTotalsView> page = await Service().ListChannelsAsync(
            Request() with { Sort = sort, Order = order },
            Ct);

        Assert.Equal(ids, string.Join(", ", page.Items.Select(c => c.Id)));
    }

    [Theory]
    [InlineData("ETUDES", "x")]
    [InlineData("hautes études", "x")]
    [InlineData("(ihes)", "x")]
    [InlineData("é", "y, x")]
    [InlineData("nothing", "")]
    public async Task The_channels_are_found_by_words_of_their_name_accents_and_case_ignored(string q, string ids)
    {
        _store.Channels = [Channel("x", "Institut des Hautes Études Scientifiques (IHES)"), Channel("y", "Émile")];

        ResultPage<ChannelTotalsView> page = await Service().ListChannelsAsync(Request() with { Q = q }, Ct);

        Assert.Equal(ids, string.Join(", ", page.Items.Select(c => c.Id)));
        Assert.Equal(page.Items.Count, page.Total);
    }

    [Fact]
    public async Task The_channels_with_videos_gone_can_be_listed_alone()
    {
        GivenThreeChannels();

        ResultPage<ChannelTotalsView> page = await Service().ListChannelsAsync(Request() with { Gone = true }, Ct);

        Assert.Equal(["c", "a"], page.Items.Select(c => c.Id));
    }

    [Fact]
    public async Task The_cursor_gives_the_next_page_until_the_last()
    {
        GivenThreeChannels();
        DashboardService service = Service();

        ResultPage<ChannelTotalsView> first = await service.ListChannelsAsync(Request() with { Size = 2 }, Ct);
        ResultPage<ChannelTotalsView> second = await service.ListChannelsAsync(
            Request() with { Size = 2, Cursor = first.NextCursor },
            Ct);

        Assert.Equal(["c", "a"], first.Items.Select(c => c.Id));
        Assert.Equal(["b"], second.Items.Select(c => c.Id));
        Assert.Equal((3L, 3L), (first.Total, second.Total));
        Assert.Null(second.NextCursor);
    }

    [Fact]
    public async Task A_cursor_of_another_sort_is_refused()
    {
        GivenThreeChannels();
        DashboardService service = Service();
        ResultPage<ChannelTotalsView> first = await service.ListChannelsAsync(Request() with { Size = 1 }, Ct);

        ValidationException error = await Assert.ThrowsAsync<ValidationException>(
            () => service.ListChannelsAsync(Request() with { Sort = "name", Cursor = first.NextCursor }, Ct));

        Assert.Equal([CursorCodec.Field], error.Errors.Select(e => e.Field));
    }

    [Fact]
    public async Task An_unknown_sort_or_order_is_refused()
    {
        ValidationException error = await Assert.ThrowsAsync<ValidationException>(
            () => Service().ListChannelsAsync(Request() with { Sort = "weight", Order = "up", Size = 0 }, Ct));

        Assert.Equal(["sort", "order", "size"], error.Errors.Select(e => e.Field));
    }

    [Fact]
    public async Task The_last_sync_is_when_the_latest_successful_run_finished()
    {
        _store.LatestSuccess = SucceededRun(hoursAfterStart: 2);

        Assert.Equal(Start.AddHours(2).AddMinutes(5), (await Service().GetAsync(Ct)).LastSync);
    }

    [Fact]
    public async Task Without_a_successful_run_there_is_no_last_sync()
    {
        Assert.Null((await Service().GetAsync(Ct)).LastSync);
    }

    private static ChannelListItem Channel(string id, string name) => new(id, name, 1000, null);

    private static DashboardChannelsRequest Request() => new(null, null, null, null, null, null);

    private void GivenThreeChannels()
    {
        _store.Channels = [Channel("a", "Alpha"), Channel("b", "Beta"), Channel("c", "Gamma")];
        _store.Stats = new LibraryStats(
            [],
            0,
            new Dictionary<string, ChannelStats>
            {
                ["a"] = new(new TypeCounts(3, 1, 0), 1, 0, 0, 2, 0),
                ["c"] = new(new TypeCounts(10, 0, 2), 4, 0, 0, 0, 5),
            });
    }

    private static TypeStats Type(VideoType type, long count, double hours) =>
        new(type, count, TimeSpan.FromHours(hours));

    private static SyncRun SucceededRun(int hoursAfterStart)
    {
        DateTimeOffset started = Start.AddHours(hoursAfterStart);
        return new SyncRun
        {
            Id = SyncRun.NewId(started),
            Mode = SyncMode.Import,
            Trigger = SyncTrigger.Startup,
            StartedAt = started,
            FinishedAt = started.AddMinutes(5),
            Outcome = SyncOutcome.Succeeded,
        };
    }

    private DashboardService Service() =>
        new(
            _store,
            _disk,
            new FakeYouTubeSettings(),
            new ImportHistoryRequestValidator(),
            ValidOptions.Create(),
            new FakeTimeProvider(Start));

    private sealed class FakeStore : IDashboardRepository
    {
        public IReadOnlyList<ChannelListItem> Channels { get; set; } = [];

        public LibraryStats Stats { get; set; } = new([], 0, new Dictionary<string, ChannelStats>());

        public SyncRun? LatestSuccess { get; set; }

        public IReadOnlyList<string> CountedChannels { get; private set; } = [];

        public Task<IReadOnlyList<ChannelListItem>> GetChannelsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Channels);

        public Task<LibraryStats> GetLibraryStatsAsync(
            IReadOnlyCollection<string> channelIds,
            CancellationToken cancellationToken)
        {
            CountedChannels = [.. channelIds];
            return Task.FromResult(Stats);
        }

        public Task<SyncRun?> GetLatestAdditionAsync(CancellationToken cancellationToken) =>
            Task.FromResult(LatestSuccess);

        public Task<IReadOnlyList<SyncRun>> GetRunsSinceAsync(
            DateTimeOffset? since,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("ImportHistoryTests reads the runs.");
    }
}
