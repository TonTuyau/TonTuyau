using Microsoft.Extensions.Time.Testing;
using TonTuyau.Application.Dashboard;
using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;
using TonTuyau.Application.UnitTests.Configuration;
using TonTuyau.Application.Validation;

namespace TonTuyau.Application.UnitTests.Dashboard;

/// <summary>
/// The import history's bars, through <see cref="DashboardService.GetImportsAsync"/>: the runs the store is asked
/// for, and how they are added up. TONTUYAU_TZ is Europe/Paris (UTC+2 in summer, +1 from 25 Oct 2026).
/// </summary>
public sealed class ImportHistoryTests
{
    /// <summary>A Monday, 12:00 in Paris.</summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeStore _store = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task By_default_the_last_30_days_a_bar_per_day_even_without_import()
    {
        _store.Runs =
        [
            Run("2026-10-12T06:00:00Z", 2, new TypeCounts(2, 0, 0)),
            Run("2026-10-12T07:00:00Z", 5, new TypeCounts(3, 1, 1)),
            Run("2026-09-20T07:00:00Z", 1, new TypeCounts(0, 1, 0)),
        ];

        ImportHistoryView history = await ImportsAsync(null);

        Assert.Equal(At("2026-09-12T22:00:00Z"), _store.Since);
        Assert.Equal((ImportStep.Day, "Europe/Paris", 8L), (history.Step, history.TimeZone, history.Total));
        Assert.Equal(30, history.Bars.Count);
        Assert.Equal(At("2026-09-12T22:00:00Z"), history.Bars[0].Start);
        Assert.Equal(new ImportBarView(At("2026-10-11T22:00:00Z"), new TypeCounts(5, 1, 1), 0), history.Bars[^1]);
        Assert.Equal(new TypeCounts(0, 1, 0), history.Bars[7].ByType);
        Assert.Equal(0, history.Bars[1].Total);
    }

    [Theory]
    [InlineData("day", ImportStep.Run, null)]
    [InlineData("week", ImportStep.Day, 7)]
    [InlineData("month", ImportStep.Day, 30)]
    [InlineData("year", ImportStep.Week, 53)]
    public async Task Each_period_has_its_step(string period, ImportStep step, int? bars)
    {
        ImportHistoryView history = await ImportsAsync(period);

        Assert.Equal(step, history.Step);
        Assert.Equal(bars ?? 0, history.Bars.Count);
    }

    [Fact]
    public async Task The_last_day_is_a_bar_per_sync_of_the_last_24_hours_an_empty_one_too()
    {
        _store.Runs =
        [
            Run("2026-10-12T09:00:00Z", 3, new TypeCounts(1, 2, 0)),
            Run("2026-10-11T12:00:00Z", 4, new TypeCounts(4, 0, 0)),
            Run("2026-10-12T08:00:00Z", 0, TypeCounts.None),
        ];

        ImportHistoryView history = await ImportsAsync("day");

        Assert.Equal(Now.AddHours(-24), _store.Since);
        Assert.Equal(
            [
                new ImportBarView(At("2026-10-11T12:00:00Z"), new TypeCounts(4, 0, 0), 0),
                new ImportBarView(At("2026-10-12T08:00:00Z"), new TypeCounts(0, 0, 0), 0),
                new ImportBarView(At("2026-10-12T09:00:00Z"), new TypeCounts(1, 2, 0), 0),
            ],
            history.Bars);
    }

    [Fact]
    public async Task Only_finished_imports_count_one_that_rebuilt_the_indices_first_too()
    {
        _store.Runs =
        [
            Run("2026-10-12T05:00:00Z", 2, new TypeCounts(2, 0, 0), finished: false),
            Run("2026-10-12T06:00:00Z", 5, new TypeCounts(5, 0, 0), rebuild: true),
            Run("2026-10-12T07:00:00Z", 3, new TypeCounts(1, 1, 1), outcome: SyncOutcome.Failed),
        ];

        ImportHistoryView history = await ImportsAsync("week");

        Assert.Equal(8, history.Total);
    }

    [Fact]
    public async Task The_runs_of_1_1_add_to_the_part_whose_types_were_not_recorded()
    {
        _store.Runs =
        [
            Run("2026-10-12T05:00:00Z", 40, byType: null),
            Run("2026-10-12T06:00:00Z", 6, new TypeCounts(3, 2, 1)),
            Run("2026-10-12T07:00:00Z", 2, byType: null),
        ];

        ImportHistoryView history = await ImportsAsync("week");

        Assert.Equal(new TypeCounts(3, 2, 1), history.Bars[^1].ByType);
        Assert.Equal((42L, 48L), (history.Bars[^1].NotRecorded, history.Bars[^1].Total));
    }

    [Fact]
    public async Task A_day_is_a_day_in_the_sync_time_zone()
    {
        _store.Runs =
        [
            Run("2026-10-10T21:59:00Z", 1, new TypeCounts(1, 0, 0)),
            Run("2026-10-10T22:01:00Z", 2, new TypeCounts(2, 0, 0)),
        ];

        ImportHistoryView history = await ImportsAsync("week");

        // 23:59 on the 10th and 00:01 on the 11th in Paris, though both are the 10th in UTC.
        Assert.Equal(
            [(At("2026-10-09T22:00:00Z"), 1L), (At("2026-10-10T22:00:00Z"), 2L)],
            history.Bars
                .Skip(4)
                .Take(2)
                .Select(b => (b.Start, b.Total)));
    }

    [Fact]
    public async Task The_days_follow_a_change_of_time()
    {
        // Paris goes from UTC+2 to UTC+1 at 03:00 on Sunday 25 Oct 2026.
        _store.Runs = [Run("2026-10-25T22:30:00Z", 1, new TypeCounts(1, 0, 0))];

        ImportHistoryView history = await ImportsAsync("week", now: At("2026-10-27T12:00:00Z"));

        Assert.Equal(
            [
                (At("2026-10-23T22:00:00Z"), 0L),
                (At("2026-10-24T22:00:00Z"), 1L),
                (At("2026-10-25T23:00:00Z"), 0L),
                (At("2026-10-26T23:00:00Z"), 0L),
            ],
            history.Bars
                .Skip(3)
                .Select(b => (b.Start, b.Total)));
    }

    [Fact]
    public async Task A_year_is_its_weeks_from_monday()
    {
        _store.Runs =
        [
            Run("2026-10-11T21:30:00Z", 1, new TypeCounts(1, 0, 0)),
            Run("2026-10-11T22:30:00Z", 2, new TypeCounts(2, 0, 0)),
            Run("2025-10-13T08:00:00Z", 4, new TypeCounts(4, 0, 0)),
        ];

        // A Wednesday: the year's first day is Wednesday 15 Oct 2025, in the week of Monday 13 Oct.
        ImportHistoryView history = await ImportsAsync("year", now: At("2026-10-14T10:00:00Z"));

        Assert.Equal(At("2025-10-12T22:00:00Z"), _store.Since);
        Assert.Equal(53, history.Bars.Count);
        Assert.Equal((At("2025-10-12T22:00:00Z"), 4L), (history.Bars[0].Start, history.Bars[0].Total));

        // 23:30 on Sunday 11 Oct in Paris is the week before; 00:30 on Monday 12 Oct opens the last week.
        Assert.Equal(
            [(At("2026-10-04T22:00:00Z"), 1L), (At("2026-10-11T22:00:00Z"), 2L)],
            history.Bars
                .TakeLast(2)
                .Select(b => (b.Start, b.Total)));
    }

    [Fact]
    public async Task All_is_every_month_from_the_first_import_empty_ones_too()
    {
        _store.Runs =
        [
            Run("2026-10-01T07:00:00Z", 2, new TypeCounts(1, 1, 0)),
            Run("2026-07-31T22:30:00Z", 40, byType: null),
            Run("2026-01-01T07:00:00Z", 0, TypeCounts.None),
        ];

        ImportHistoryView history = await ImportsAsync("all");

        Assert.Null(_store.Since);
        Assert.Equal(ImportStep.Month, history.Step);

        // 00:30 on 1 Aug in Paris: August, not July.
        Assert.Equal(
            [
                (At("2026-07-31T22:00:00Z"), 40L),
                (At("2026-08-31T22:00:00Z"), 0L),
                (At("2026-09-30T22:00:00Z"), 2L),
            ],
            history.Bars.Select(b => (b.Start, b.Total)));
    }

    [Fact]
    public async Task Without_any_import_all_has_no_bar()
    {
        _store.Runs = [Run("2026-10-01T07:00:00Z", 0, TypeCounts.None)];

        Assert.Empty((await ImportsAsync("all")).Bars);
    }

    [Theory]
    [InlineData("hour")]
    [InlineData("Month")]
    [InlineData("")]
    public async Task An_unknown_period_is_refused(string period)
    {
        ValidationException error = await Assert.ThrowsAsync<ValidationException>(() => ImportsAsync(period));

        Assert.Equal(["period"], error.Errors.Select(e => e.Field));
        Assert.Null(_store.Since);
    }

    private static DateTimeOffset At(string instant) =>
        DateTimeOffset.Parse(instant, System.Globalization.CultureInfo.InvariantCulture);

    private static SyncRun Run(
        string started,
        int created,
        TypeCounts? byType,
        SyncOutcome outcome = SyncOutcome.Succeeded,
        bool finished = true,
        bool rebuild = false)
    {
        DateTimeOffset start = At(started);
        return new SyncRun
        {
            Id = SyncRun.NewId(start),
            Mode = SyncMode.Import,
            Trigger = SyncTrigger.Startup,
            StartedAt = start,
            FinishedAt = finished ? start.AddMinutes(5) : null,
            Outcome = finished ? outcome : SyncOutcome.Running,
            IndexRebuild = rebuild,
            Counts = new SyncCounts { Created = created, CreatedByType = byType },
        };
    }

    private Task<ImportHistoryView> ImportsAsync(string? period, DateTimeOffset? now = null)
    {
        DashboardService service = new(
            _store,
            new ImportHistoryRequestValidator(),
            ValidOptions.Create(),
            new FakeTimeProvider(now ?? Now));
        return service.GetImportsAsync(new ImportHistoryRequest(period), Ct);
    }

    private sealed class FakeStore : IDashboardRepository
    {
        public IReadOnlyList<SyncRun> Runs { get; set; } = [];

        public DateTimeOffset? Since { get; private set; }

        public Task<IReadOnlyList<SyncRun>> GetRunsSinceAsync(
            DateTimeOffset? since,
            CancellationToken cancellationToken)
        {
            Since = since;
            return Task.FromResult(Runs);
        }

        public Task<IReadOnlyList<ChannelListItem>> GetChannelsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<LibraryStats> GetLibraryStatsAsync(
            IReadOnlyCollection<string> channelIds,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SyncRun?> GetLatestAdditionAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
