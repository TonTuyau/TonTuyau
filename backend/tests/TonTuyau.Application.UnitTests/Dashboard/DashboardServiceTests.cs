using Microsoft.Extensions.Time.Testing;
using TonTuyau.Application.Dashboard;
using TonTuyau.Application.Library;
using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;
using TonTuyau.Application.UnitTests.Configuration;

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
                ["a"] = new(new TypeCounts(3, 1, 0), 1, 0, 0, 2, 0),
                ["c"] = new(new TypeCounts(10, 0, 2), 4, 0, 0, 0, 5),
            });

        IReadOnlyList<ChannelTotalsView> channels = (await Service().GetAsync(Ct)).Channels;

        Assert.Equal(
            [("c", 12L, 4L, 0L, 5L), ("a", 4L, 1L, 2L, 0L), ("b", 0L, 0L, 0L, 0L)],
            channels.Select(c => (c.Id, c.Total, c.Gone, c.RetryLater, c.NotChecked)));
        Assert.Equal(new TypeCounts(10, 0, 2), channels[0].Counts);
        Assert.Equal(TypeCounts.None, channels[2].Counts);
        Assert.Equal(["a", "b", "c"], _store.CountedChannels);
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

        public Task<SyncRun?> GetLatestSuccessfulRunAsync(CancellationToken cancellationToken) =>
            Task.FromResult(LatestSuccess);

        public Task<IReadOnlyList<SyncRun>> GetRunsSinceAsync(
            DateTimeOffset? since,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("ImportHistoryTests reads the runs.");
    }
}
