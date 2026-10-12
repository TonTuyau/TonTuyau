using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using TonTuyau.Api.Hosting;
using TonTuyau.Application.Configuration;
using TonTuyau.Application.Sync;

namespace TonTuyau.Api.UnitTests;

public sealed class SyncSchedulerTests
{
    // Friday 2026-10-02, 12:30 in Paris (UTC+2).
    private static readonly DateTimeOffset Start = new(2026, 10, 2, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Runs_each_sync_when_it_is_due()
    {
        TimerCountingClock clock = new(Start);
        ConcurrentQueue<(SyncMode Mode, DateTimeOffset At)> runs = new();
        RecordingLogger log = new();
        using SyncScheduler scheduler = Scheduler(
            clock,
            (mode, _) =>
            {
                runs.Enqueue((mode, clock.GetUtcNow()));
                return Task.FromResult(Run(mode));
            },
            logger: log);

        // Step by step: wait until the scheduler waits for its next run (its delay's timer exists), then move the clock
        // to exactly that time and wait for the run. (Stepping minute by minute raced the scheduler's continuations on
        // a busy machine; moving the clock right after the "next" log, before the timer existed, set that timer from
        // the new time, so it never fired: seen on a CI runner.)
        await scheduler.StartAsync(TestContext.Current.CancellationToken);
        for (int count = 1; count <= 12; count++)
        {
            await WaitUntilAsync(() => log.Count("Next scheduled sync") >= count && clock.TimersCreated >= count);
            clock.SetUtcNow(log.LastNextAt);
            await WaitUntilAsync(() => runs.Count >= count);
        }

        await scheduler.StopAsync(TestContext.Current.CancellationToken);

        (SyncMode Mode, DateTimeOffset At)[] all = runs.ToArray();
        Assert.Equal((SyncMode.Incremental, Start.AddMinutes(30)), all[0]);       // 13:00 Paris
        Assert.Equal((SyncMode.Full, new DateTimeOffset(2026, 10, 2, 23, 0, 0, TimeSpan.Zero)), all[11]); // Saturday 01:00
    }

    [Fact]
    public async Task A_busy_lock_skips_the_tick_and_the_scheduler_keeps_going()
    {
        FakeTimeProvider clock = new(Start);
        int calls = 0;
        using SyncScheduler scheduler = Scheduler(
            clock,
            (mode, _) =>
            {
                Interlocked.Increment(ref calls);
                throw new SyncBusyException(null);
            });

        await scheduler.StartAsync(TestContext.Current.CancellationToken);
        await AdvanceUntilAsync(clock, () => Volatile.Read(ref calls) >= 2, TimeSpan.FromHours(2));
        await scheduler.StopAsync(TestContext.Current.CancellationToken);

        Assert.True(calls >= 2);
    }

    [Fact]
    public async Task Disabled_scheduler_never_runs_anything()
    {
        FakeTimeProvider clock = new(Start);
        int calls = 0;
        using SyncScheduler scheduler = Scheduler(
            clock,
            (mode, _) =>
            {
                calls++;
                return Task.FromResult(Run(mode));
            },
            enabled: false);

        await scheduler.StartAsync(TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromDays(8));
        await Task.Delay(100, TestContext.Current.CancellationToken);
        await scheduler.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, calls);
    }

    private static SyncScheduler Scheduler(
        FakeTimeProvider clock,
        Func<SyncMode, CancellationToken, Task<SyncRun>> run,
        bool enabled = true,
        ILogger<SyncScheduler>? logger = null)
    {
        SyncOptions sync = new(
            TonTuyauOptionsReader.DefaultIncrementalCron,
            TonTuyauOptionsReader.DefaultFullCron,
            "Europe/Paris",
            SchedulerEnabled: enabled);
        TonTuyauOptions options = new(
            new TubeArchivistOptions("http://ta.test", "t"),
            new ElasticsearchOptions("http://es.test", "elastic", "p"),
            new StorageOptions("/youtube", "/data/art"),
            sync,
            new DatabaseOptions("db.test", "tontuyau", "tontuyau", "p"),
            new AccountOptions("admin", "", true));
        return new SyncScheduler(
            new SyncSchedule(sync),
            run,
            options,
            clock,
            logger ?? NullLogger<SyncScheduler>.Instance);
    }

    private static SyncRun Run(SyncMode mode) => new()
    {
        Id = SyncRun.NewId(DateTimeOffset.UtcNow),
        Mode = mode,
        Trigger = SyncTrigger.Scheduler,
        StartedAt = DateTimeOffset.UtcNow,
        Outcome = SyncOutcome.Succeeded,
    };

    /// <summary>Moves the fake clock minute by minute (letting the scheduler's continuations run) until the condition holds.</summary>
    private static async Task AdvanceUntilAsync(FakeTimeProvider clock, Func<bool> condition, TimeSpan limit)
    {
        for (TimeSpan elapsed = TimeSpan.Zero; elapsed <= limit && !condition(); elapsed += TimeSpan.FromMinutes(1))
        {
            clock.Advance(TimeSpan.FromMinutes(1));
            for (int i = 0; i < 50 && !condition(); i++)
            {
                await Task.Delay(1);
            }
        }

        Assert.True(condition(), "condition not reached");
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTime limit = DateTime.UtcNow.AddSeconds(30);
        while (!condition() && DateTime.UtcNow < limit)
        {
            await Task.Delay(5);
        }

        Assert.True(condition(), "condition not reached");
    }

    /// <summary>A fake clock that counts the timers made on it (each scheduler wait makes one).</summary>
    private sealed class TimerCountingClock(DateTimeOffset start) : FakeTimeProvider(start)
    {
        private int _timersCreated;

        public int TimersCreated => Volatile.Read(ref _timersCreated);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            ITimer timer = base.CreateTimer(callback, state, dueTime, period);
            Interlocked.Increment(ref _timersCreated);
            return timer;
        }
    }

    /// <summary>Keeps the scheduler's messages, and the time of the last "next scheduled sync".</summary>
    private sealed class RecordingLogger : ILogger<SyncScheduler>
    {
        private readonly ConcurrentQueue<string> _messages = new();

        public DateTimeOffset LastNextAt { get; private set; }

        public int Count(string start) => _messages.Count(m => m.StartsWith(start, StringComparison.Ordinal));

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (state is IReadOnlyList<KeyValuePair<string, object?>> values
                && values.FirstOrDefault(v => v.Key == "At").Value is DateTimeOffset at)
            {
                LastNextAt = at;
            }

            _messages.Enqueue(formatter(state, exception));
        }
    }
}
