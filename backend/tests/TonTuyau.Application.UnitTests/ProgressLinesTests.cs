using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Time.Testing;
using TonTuyau.Application.Sync;

namespace TonTuyau.Application.UnitTests;

public sealed partial class ProgressLinesTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Each_update_is_a_line_of_its_own_terminal_or_not()
    {
        FakeTimeProvider clock = new(Start);
        using StringWriter output = new();
        using ProgressLines progress = new(output.WriteLine, clock);

        progress.Report(Progress(SyncPhase.Channels, 0, 150));
        clock.Advance(TimeSpan.FromSeconds(8));
        progress.Report(Progress(SyncPhase.ChannelVideos, 0, 150));
        progress.Finish();

        string expected = """
            [00:00] channels      0 / 150 read
            [00:08] videos        0 / 150 channels · 0 videos

            """;
        Assert.Equal(expected.ReplaceLineEndings(Environment.NewLine), output.ToString());
        // QA16-1: no rewriting in place, no ANSI sequence.
        Assert.DoesNotContain("\r", output.ToString().Replace(Environment.NewLine, "\n", StringComparison.Ordinal));
        Assert.DoesNotContain("\u001b", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Updates_within_the_interval_are_skipped()
    {
        FakeTimeProvider clock = new(Start);
        using StringWriter output = new();
        using ProgressLines progress = new(output.WriteLine, clock);

        progress.Report(Progress(SyncPhase.Channels, 0, 150));
        clock.Advance(TimeSpan.FromSeconds(5));
        progress.Report(Progress(SyncPhase.Channels, 10, 150));
        clock.Advance(ProgressLines.Interval - TimeSpan.FromSeconds(5) - TimeSpan.FromMilliseconds(1));
        progress.Report(Progress(SyncPhase.Channels, 20, 150));
        clock.Advance(TimeSpan.FromMilliseconds(1)); // 00:10: a line is due, the heartbeat shows the latest state
        progress.Report(Progress(SyncPhase.Channels, 30, 150)); // just after it: skipped
        clock.Advance(TimeSpan.FromSeconds(1));
        progress.Finish();

        Assert.Equal(
            [
                "[00:00] channels      0 / 150 read",
                "[00:10] channels      20 / 150 read",
                "[00:11] channels      30 / 150 read", // the last state, at the end
            ],
            Lines(output));
    }

    [Fact]
    public void A_phase_change_is_shown_at_once()
    {
        FakeTimeProvider clock = new(Start);
        using StringWriter output = new();
        using ProgressLines progress = new(output.WriteLine, clock);

        progress.Report(Progress(SyncPhase.ChannelVideos, 120, 120));
        clock.Advance(TimeSpan.FromSeconds(1));
        progress.Report(Progress(SyncPhase.MediaCheck, 0, null));
        progress.Report(Progress(SyncPhase.Deletions, 0, 0));

        Assert.Equal(
            [
                "[00:00] videos        120 / 120 channels · 0 videos",
                "[00:01] media check   0 files checked · 0 missing",
                "[00:01] deletions     nothing to delete",
            ],
            Lines(output));
    }

    [Fact]
    public void A_phase_ends_with_its_last_state()
    {
        FakeTimeProvider clock = new(Start);
        using StringWriter output = new();
        using ProgressLines progress = new(output.WriteLine, clock);

        progress.Report(Progress(SyncPhase.Channels, 0, 150));
        clock.Advance(TimeSpan.FromSeconds(3));
        progress.Report(Progress(SyncPhase.Channels, 150, 150));
        progress.Report(Progress(SyncPhase.ChannelVideos, 0, 150));

        Assert.Equal(
            [
                "[00:00] channels      0 / 150 read",
                "[00:03] channels      150 / 150 read",
                "[00:03] videos        0 / 150 channels · 0 videos",
            ],
            Lines(output));
    }

    [Fact]
    public void Finishing_shows_the_last_state_once_if_it_was_skipped()
    {
        FakeTimeProvider clock = new(Start);
        using StringWriter output = new();
        using ProgressLines progress = new(output.WriteLine, clock);

        progress.Report(Progress(SyncPhase.ArtRetry, 0, 3));
        clock.Advance(TimeSpan.FromSeconds(2));
        progress.Report(Progress(SyncPhase.ArtRetry, 3, 3));
        progress.Finish();
        progress.Finish();

        Assert.Equal(
            ["[00:00] art retry     0 / 3 images retried", "[00:02] art retry     3 / 3 images retried"],
            Lines(output));
    }

    [Fact]
    public void A_last_state_that_would_only_repeat_the_line_is_not_shown_again()
    {
        FakeTimeProvider clock = new(Start);
        using StringWriter output = new();
        using ProgressLines progress = new(output.WriteLine, clock);

        progress.Report(Progress(SyncPhase.MediaCheck, 0, null));
        clock.Advance(TimeSpan.FromSeconds(1));
        // A count the line doesn't show changes.
        progress.Report(Progress(SyncPhase.MediaCheck, 0, null) with { Counts = new SyncCounts { MediaMissing = 1 } });
        progress.Report(Progress(SyncPhase.Deletions, 0, 0));
        progress.Report(Progress(SyncPhase.Deletions, 0, 0) with { Counts = new SyncCounts { Rejected = 1 } });
        progress.Finish();

        Assert.Equal(
            ["[00:00] media check   0 files checked · 0 missing", "[00:01] deletions     nothing to delete"],
            Lines(output));
    }

    [Fact]
    public void Finishing_without_any_progress_prints_nothing()
    {
        using StringWriter output = new();
        using ProgressLines progress = new(output.WriteLine, new FakeTimeProvider(Start));

        progress.Finish();

        Assert.Equal("", output.ToString());
    }

    // A first full sync of the real library: about 20 minutes, a report every 100 ms (12,000), 6 phases. The throttle
    // keeps it to about 130 lines, not thousands. Each 10 s mark shows the latest state (the heartbeat's timer is due
    // just before the report of that instant).
    [Fact]
    public void A_twenty_minute_run_prints_about_a_hundred_and_thirty_lines()
    {
        FakeTimeProvider clock = new(Start);
        using StringWriter output = new();
        using ProgressLines progress = new(output.WriteLine, clock);
        SyncPhase[] phases =
        [
            SyncPhase.Channels,
            SyncPhase.ChannelVideos,
            SyncPhase.MediaCheck,
            SyncPhase.Deletions,
            SyncPhase.ArtRetry,
            SyncPhase.IndexSwitch,
        ];

        for (int tick = 0; tick < 12_000; tick++)
        {
            // Channels for the first minute, videos until 19:00, then the short phases.
            SyncPhase phase = tick < 600 ? phases[0] : tick < 11_400 ? phases[1] : phases[2 + ((tick - 11_400) / 150)];
            progress.Report(Progress(phase, tick, null));
            clock.Advance(TimeSpan.FromMilliseconds(100));
        }

        progress.Finish();

        string[] lines = Lines(output);
        Assert.InRange(lines.Length, 100, 150);
        Assert.Equal(
            [
                "[00:50] channels      499 read",
                "[01:00] channels      599 read", // the phase's last state
                "[01:00] videos        600 channels · 0 videos",
                "[01:10] videos        699 channels · 0 videos",
            ],
            lines[5..9]);
        Assert.Equal("[19:55] index switch  switching to the new indices", lines[^1]);
    }

    // The sync reports from several threads at once (the art resizing), while the heartbeat may fire: every line must
    // come out whole, never two interleaved.
    [Fact]
    public async Task Reports_from_many_threads_come_out_whole()
    {
        const int Threads = 8;
        const int PerThread = 2_000;
        FakeTimeProvider clock = new(Start);
        using StringWriter output = new();
        using ProgressLines progress = new(output.WriteLine, clock);

        using Barrier barrier = new(Threads);
        Task[] workers = Enumerable.Range(0, Threads)
            .Select(thread => Task.Run(
                () =>
                {
                    barrier.SignalAndWait(TestContext.Current.CancellationToken);
                    for (int i = 0; i < PerThread; i++)
                    {
                        // Each thread also moves the clock (firing the heartbeat now and then) and changes phase,
                        // so lines are written all along.
                        clock.Advance(TimeSpan.FromMilliseconds(i % 100 == 0 ? 10_000 : 5));
                        SyncPhase phase = i % 500 == 0 ? SyncPhase.ArtRetry : SyncPhase.ChannelVideos;
                        progress.Report(Progress(phase, (thread * PerThread) + i, null));
                    }
                },
                TestContext.Current.CancellationToken))
            .ToArray();
        await Task.WhenAll(workers);
        progress.Finish();

        string[] lines = Lines(output);
        Assert.True(lines.Length > 10, $"{lines.Length} lines");
        Assert.All(lines, line => Assert.Matches(WholeLine(), line));
    }

    // QA16-3 b: a long step with nothing new to report still shows the run is alive.
    [Fact]
    public void A_long_step_with_nothing_new_is_shown_again_every_interval()
    {
        FakeTimeProvider clock = new(Start);
        using StringWriter output = new();
        using ProgressLines progress = new(output.WriteLine, clock);

        progress.Report(Progress(SyncPhase.Channels, 150, 150));
        progress.Report(Progress(SyncPhase.IndexSwitch, 0, null));
        Wait(clock, TimeSpan.FromSeconds(9));
        string[] beforeTheFirstBeat = Lines(output);
        Wait(clock, TimeSpan.FromSeconds(26));
        progress.Finish();

        Assert.Equal(2, beforeTheFirstBeat.Length);
        Assert.Equal(
            [
                "[00:00] channels      150 / 150 read",
                "[00:00] index switch  switching to the new indices",
                "[00:10] index switch  switching to the new indices",
                "[00:20] index switch  switching to the new indices",
                "[00:30] index switch  switching to the new indices",
            ],
            Lines(output));
    }

    [Fact]
    public void The_heartbeat_keeps_an_unknown_total_unknown()
    {
        FakeTimeProvider clock = new(Start);
        using StringWriter output = new();
        using ProgressLines progress = new(output.WriteLine, clock);

        progress.Report(Progress(SyncPhase.ChannelVideos, 12, null));
        Wait(clock, TimeSpan.FromSeconds(10));

        Assert.Equal(
            ["[00:00] videos        12 channels · 0 videos", "[00:10] videos        12 channels · 0 videos"],
            Lines(output));
    }

    [Fact]
    public void After_a_line_the_heartbeat_waits_a_whole_interval()
    {
        FakeTimeProvider clock = new(Start);
        using StringWriter output = new();
        using ProgressLines progress = new(output.WriteLine, clock);

        progress.Report(Progress(SyncPhase.ChannelVideos, 1, null));
        Wait(clock, TimeSpan.FromSeconds(14)); // the heartbeat: [00:10]
        progress.Report(Progress(SyncPhase.ChannelVideos, 2, null)); // within the interval: skipped
        Wait(clock, TimeSpan.FromSeconds(9)); // the heartbeat shows it: [00:20]
        progress.Report(Progress(SyncPhase.MediaCheck, 0, null)); // a phase: at once, [00:23]
        Wait(clock, TimeSpan.FromSeconds(9.9)); // not yet

        Assert.Equal(
            [
                "[00:00] videos        1 channels · 0 videos",
                "[00:10] videos        1 channels · 0 videos",
                "[00:20] videos        2 channels · 0 videos",
                "[00:23] media check   0 files checked · 0 missing",
            ],
            Lines(output));
    }

    // Updates every 2 s already give a line every 10 s: the heartbeat adds none (the line due at a 10 s mark shows the
    // latest state, the one of 2 s before when the timer is due just before that instant's update).
    [Fact]
    public void Updates_every_few_seconds_get_no_extra_lines()
    {
        FakeTimeProvider clock = new(Start);
        using StringWriter output = new();
        using ProgressLines progress = new(output.WriteLine, clock);

        for (int second = 0; second <= 60; second += 2)
        {
            progress.Report(Progress(SyncPhase.ChannelVideos, second, null));
            clock.Advance(TimeSpan.FromSeconds(2));
        }

        Assert.Equal(
            [
                "[00:00] videos        0 channels · 0 videos",
                "[00:10] videos        8 channels · 0 videos",
                "[00:20] videos        18 channels · 0 videos",
                "[00:30] videos        28 channels · 0 videos",
                "[00:40] videos        38 channels · 0 videos",
                "[00:50] videos        48 channels · 0 videos",
                "[01:00] videos        58 channels · 0 videos",
            ],
            Lines(output));
    }

    // Whatever the rhythm of the updates, two lines of one phase are never closer than the interval, and a line comes
    // at least every interval.
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(13)]
    [InlineData(45)]
    public void Lines_of_one_phase_come_once_per_interval(int everySeconds)
    {
        FakeTimeProvider clock = new(Start);
        using StringWriter output = new();
        using ProgressLines progress = new(output.WriteLine, clock);

        for (int second = 0; second < 300; second++)
        {
            if (second % everySeconds == 0)
            {
                progress.Report(Progress(SyncPhase.ChannelVideos, second, null));
            }

            clock.Advance(TimeSpan.FromSeconds(1));
        }

        TimeSpan[] times = Lines(output)
            .Select(line => TimeSpan.ParseExact(line[1..6], @"mm\:ss", CultureInfo.InvariantCulture))
            .ToArray();
        Assert.All(times.Zip(times.Skip(1)), pair => Assert.Equal(ProgressLines.Interval, pair.Second - pair.First));
        Assert.Equal(31, times.Length);
    }

    [Fact]
    public void Nothing_reported_means_no_heartbeat()
    {
        FakeTimeProvider clock = new(Start);
        using StringWriter output = new();
        using ProgressLines progress = new(output.WriteLine, clock);

        clock.Advance(TimeSpan.FromMinutes(1));
        progress.Finish();

        Assert.Equal("", output.ToString());
    }

    private static SyncProgress Progress(SyncPhase phase, int done, int? total) =>
        new(phase, Start, done, total, new SyncCounts(), 0, 0);

    // Lets time go by as a real clock does, a second at a time (one big Advance fires a timer once, at the end).
    private static void Wait(FakeTimeProvider clock, TimeSpan time)
    {
        for (TimeSpan waited = TimeSpan.Zero; waited < time; waited += TimeSpan.FromSeconds(1))
        {
            clock.Advance(time - waited < TimeSpan.FromSeconds(1) ? time - waited : TimeSpan.FromSeconds(1));
        }
    }

    private static string[] Lines(StringWriter output) =>
        output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

    // One progress line and nothing else: time (mm:ss or h:mm:ss), label, detail.
    [GeneratedRegex(@"\A\[(\d+:)?\d\d:\d\d\] [a-z ]{13} [^\[\r\n\u001b]+\z")]
    private static partial Regex WholeLine();
}
