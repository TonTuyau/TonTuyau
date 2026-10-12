using TonTuyau.Application.Dashboard;
using TonTuyau.Application.Library;
using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;

namespace TonTuyau.Application.UnitTests.Dashboard;

/// <summary>What the dashboard computes from the store's counts: <see cref="IDashboardRepository"/> is a fake.</summary>
public sealed class DashboardServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeStore _store = new();

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
    public async Task Every_channel_is_listed_biggest_first_even_without_videos()
    {
        _store.Channels = [Channel("a", "Alpha"), Channel("b", "Beta"), Channel("c", "Gamma")];
        _store.Stats = new LibraryStats(
            [],
            0,
            new Dictionary<string, ChannelStats>
            {
                ["a"] = new(new TypeCounts(3, 1, 0), 1),
                ["c"] = new(new TypeCounts(10, 0, 2), 4),
            });

        IReadOnlyList<ChannelTotalsView> channels = (await Service().GetAsync(Ct)).Channels;

        Assert.Equal(
            [("c", 12L, 4L), ("a", 4L, 1L), ("b", 0L, 0L)],
            channels.Select(c => (c.Id, c.Total, c.Gone)));
        Assert.Equal(new TypeCounts(10, 0, 2), channels[0].Counts);
        Assert.Equal(TypeCounts.None, channels[2].Counts);
        Assert.Equal(["a", "b", "c"], _store.CountedChannels);
    }

    [Fact]
    public async Task The_imports_are_the_finished_runs_that_brought_videos_oldest_first()
    {
        // Most recent first, as the store gives them.
        _store.Runs =
        [
            Run(hoursAfterStart: 6, created: 2, byType: new TypeCounts(1, 1, 0), finished: false),
            Run(hoursAfterStart: 5, created: 9, byType: new TypeCounts(6, 2, 1)),
            Run(hoursAfterStart: 4, created: 0, byType: TypeCounts.None),
            Run(hoursAfterStart: 3, created: 45_000, byType: new TypeCounts(45_000, 0, 0), rebuild: true),
            Run(hoursAfterStart: 2, created: 3, byType: null, outcome: SyncOutcome.Failed),
        ];

        IReadOnlyList<ImportView> imports = (await Service().GetAsync(Ct)).Imports;

        Assert.Equal(
            [
                new ImportView(Start.AddHours(2), 3, null),
                new ImportView(Start.AddHours(5), 9, new TypeCounts(6, 2, 1)),
            ],
            imports);
        Assert.Equal(DashboardService.RunsRead, _store.RunsAskedFor);
    }

    [Fact]
    public async Task The_import_history_keeps_the_latest_30()
    {
        _store.Runs = [.. Enumerable.Range(0, 40).Select(i => Run(hoursAfterStart: 40 - i, created: i + 1))];

        IReadOnlyList<ImportView> imports = (await Service().GetAsync(Ct)).Imports;

        Assert.Equal(DashboardService.ImportsShown, imports.Count);
        Assert.Equal((Start.AddHours(11), 30), (imports[0].At, imports[0].Total));
        Assert.Equal((Start.AddHours(40), 1), (imports[^1].At, imports[^1].Total));
    }

    [Fact]
    public async Task The_last_sync_is_the_latest_successful_run()
    {
        _store.Runs =
        [
            Run(hoursAfterStart: 3, created: 0, outcome: SyncOutcome.Failed),
            Run(hoursAfterStart: 2, created: 0),
            Run(hoursAfterStart: 1, created: 0),
        ];

        Assert.Equal(Start.AddHours(2).AddMinutes(5), (await Service().GetAsync(Ct)).LastSync);
    }

    [Fact]
    public async Task Without_a_successful_run_there_is_no_last_sync()
    {
        _store.Runs = [Run(hoursAfterStart: 1, created: 0, outcome: SyncOutcome.Failed)];

        Assert.Null((await Service().GetAsync(Ct)).LastSync);
    }

    private static ChannelListItem Channel(string id, string name) => new(id, name, 1000, null);

    private static TypeStats Type(VideoType type, long count, double hours) =>
        new(type, count, TimeSpan.FromHours(hours));

    private static SyncRun Run(
        int hoursAfterStart,
        int created,
        TypeCounts? byType = null,
        SyncOutcome outcome = SyncOutcome.Succeeded,
        bool finished = true,
        bool rebuild = false)
    {
        DateTimeOffset started = Start.AddHours(hoursAfterStart);
        return new SyncRun
        {
            Id = SyncRun.NewId(started),
            Mode = SyncMode.Incremental,
            Trigger = SyncTrigger.Scheduler,
            StartedAt = started,
            FinishedAt = finished ? started.AddMinutes(5) : null,
            Outcome = finished ? outcome : SyncOutcome.Running,
            IndexRebuild = rebuild,
            Counts = new SyncCounts { Created = created, CreatedByType = byType },
        };
    }

    private DashboardService Service() => new(_store);

    private sealed class FakeStore : IDashboardRepository
    {
        public IReadOnlyList<ChannelListItem> Channels { get; set; } = [];

        public LibraryStats Stats { get; set; } = new([], 0, new Dictionary<string, ChannelStats>());

        public IReadOnlyList<SyncRun> Runs { get; set; } = [];

        public IReadOnlyList<string> CountedChannels { get; private set; } = [];

        public int RunsAskedFor { get; private set; }

        public Task<IReadOnlyList<ChannelListItem>> GetChannelsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Channels);

        public Task<LibraryStats> GetLibraryStatsAsync(
            IReadOnlyCollection<string> channelIds,
            CancellationToken cancellationToken)
        {
            CountedChannels = [.. channelIds];
            return Task.FromResult(Stats);
        }

        public Task<IReadOnlyList<SyncRun>> GetRecentRunsAsync(int count, CancellationToken cancellationToken)
        {
            RunsAskedFor = count;
            return Task.FromResult(Runs);
        }
    }
}
