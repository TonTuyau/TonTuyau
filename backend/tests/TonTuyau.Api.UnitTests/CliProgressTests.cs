using System.Text.RegularExpressions;
using Microsoft.Extensions.Time.Testing;
using TonTuyau.Api.Cli;
using TonTuyau.Application.Sync;

namespace TonTuyau.Api.UnitTests;

public sealed partial class CliProgressTests
{
    private const string Clear = "\u001b[K";

    private static readonly DateTimeOffset Start = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Without_a_terminal_each_update_is_a_line_of_its_own()
    {
        FakeTimeProvider clock = new(Start);
        using StringWriter output = new();
        CliProgress progress = new(output, inPlace: false, clock);

        progress.Report(Progress(SyncPhase.Channels, 0, 150));
        clock.Advance(TimeSpan.FromSeconds(42));
        progress.Report(Progress(SyncPhase.ChannelVideos, 0, 150));
        progress.Finish();

        string expected = """
            [00:00] channels      0 / 150 read
            [00:42] videos        0 / 150 channels · 0 videos

            """;
        Assert.Equal(expected.ReplaceLineEndings(Environment.NewLine), output.ToString());
        Assert.DoesNotContain("\r", output.ToString().Replace(Environment.NewLine, "\n", StringComparison.Ordinal));
    }

    [Fact]
    public void On_a_terminal_one_line_is_rewritten_in_place_then_ended_before_the_report()
    {
        FakeTimeProvider clock = new(Start);
        using StringWriter output = new();
        CliProgress progress = new(output, inPlace: true, clock);

        progress.Report(Progress(SyncPhase.Channels, 0, 150));
        clock.Advance(TimeSpan.FromSeconds(10));
        progress.Report(Progress(SyncPhase.Channels, 100, 150));
        string beforeFinish = output.ToString();
        progress.Finish();

        Assert.Equal(
            $"\r[00:00] channels      0 / 150 read{Clear}\r[00:10] channels      100 / 150 read{Clear}",
            beforeFinish);
        Assert.Equal(beforeFinish + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void On_a_terminal_a_line_wider_than_it_is_cut_so_it_never_wraps()
    {
        using StringWriter output = new();
        CliProgress progress = new(output, inPlace: true, new FakeTimeProvider(Start), width: 20);

        progress.Report(Progress(SyncPhase.Channels, 100, 150));

        Assert.Equal($"\r[00:00] channels   {Clear}", output.ToString());
    }

    [Fact]
    public void Updates_within_the_interval_are_skipped()
    {
        FakeTimeProvider clock = new(Start);
        using StringWriter output = new();
        CliProgress progress = new(output, inPlace: false, clock);

        progress.Report(Progress(SyncPhase.Channels, 0, 150));
        clock.Advance(TimeSpan.FromSeconds(5));
        progress.Report(Progress(SyncPhase.Channels, 10, 150));
        clock.Advance(CliProgress.Interval - TimeSpan.FromSeconds(5) - TimeSpan.FromMilliseconds(1));
        progress.Report(Progress(SyncPhase.Channels, 20, 150));
        clock.Advance(TimeSpan.FromMilliseconds(1));
        progress.Report(Progress(SyncPhase.Channels, 30, 150));

        Assert.Equal(["[00:00] channels      0 / 150 read", "[00:10] channels      30 / 150 read"], Lines(output));
    }

    [Fact]
    public void A_phase_change_is_shown_at_once()
    {
        FakeTimeProvider clock = new(Start);
        using StringWriter output = new();
        CliProgress progress = new(output, inPlace: false, clock);

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
    public void Without_a_terminal_a_phase_ends_with_its_last_state()
    {
        FakeTimeProvider clock = new(Start);
        using StringWriter output = new();
        CliProgress progress = new(output, inPlace: false, clock);

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
    public void On_a_terminal_the_next_phase_simply_replaces_the_line()
    {
        FakeTimeProvider clock = new(Start);
        using StringWriter output = new();
        CliProgress progress = new(output, inPlace: true, clock);

        progress.Report(Progress(SyncPhase.Channels, 0, 150));
        clock.Advance(TimeSpan.FromSeconds(3));
        progress.Report(Progress(SyncPhase.Channels, 150, 150));
        progress.Report(Progress(SyncPhase.ChannelVideos, 0, 150));

        Assert.Equal(
            $"\r[00:00] channels      0 / 150 read{Clear}\r[00:03] videos        0 / 150 channels · 0 videos{Clear}",
            output.ToString());
    }

    [Fact]
    public void Finishing_shows_the_last_state_once_if_it_was_skipped()
    {
        FakeTimeProvider clock = new(Start);
        using StringWriter output = new();
        CliProgress progress = new(output, inPlace: false, clock);

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
        CliProgress progress = new(output, inPlace: false, clock);

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
        CliProgress progress = new(output, inPlace: true, new FakeTimeProvider(Start));

        progress.Finish();

        Assert.Equal("", output.ToString());
    }

    // A first full sync of the real library: about 20 minutes, a report every 100 ms (12,000), 6 phases. The throttle
    // keeps it to about 130 lines, not thousands.
    [Fact]
    public void A_twenty_minute_run_prints_about_a_hundred_and_thirty_lines()
    {
        FakeTimeProvider clock = new(Start);
        using StringWriter output = new();
        CliProgress progress = new(output, inPlace: false, clock);
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
                "[00:50] channels      500 read",
                "[01:00] channels      599 read", // the phase's last state
                "[01:00] videos        600 channels · 0 videos",
                "[01:10] videos        700 channels · 0 videos",
            ],
            lines[5..9]);
        Assert.Equal("[19:55] index switch  switching to the new indices", lines[^1]);
    }

    // The sync reports from several threads at once (the art resizing): every line must come out whole, never two
    // interleaved, and the in-place line must be ended exactly once.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reports_from_many_threads_come_out_whole(bool inPlace)
    {
        const int Threads = 8;
        const int PerThread = 2_000;
        FakeTimeProvider clock = new(Start);
        using StringWriter output = new();
        CliProgress progress = new(output, inPlace, clock);

        using Barrier barrier = new(Threads);
        Task[] workers = Enumerable.Range(0, Threads)
            .Select(thread => Task.Run(
                () =>
                {
                    barrier.SignalAndWait(TestContext.Current.CancellationToken);
                    for (int i = 0; i < PerThread; i++)
                    {
                        // Each thread also moves the clock and changes phase now and then, so lines are written
                        // all along.
                        clock.Advance(TimeSpan.FromMilliseconds(5));
                        SyncPhase phase = i % 500 == 0 ? SyncPhase.ArtRetry : SyncPhase.ChannelVideos;
                        progress.Report(Progress(phase, (thread * PerThread) + i, null));
                    }
                },
                TestContext.Current.CancellationToken))
            .ToArray();
        await Task.WhenAll(workers);
        progress.Finish();

        string text = output.ToString();
        string[] lines = inPlace
            ? text.TrimEnd().Split('\r', StringSplitOptions.RemoveEmptyEntries)
            : Lines(output);
        Assert.True(lines.Length > 10, $"{lines.Length} lines");
        Assert.All(lines, line => Assert.Matches(WholeLine(), inPlace ? line.TrimEnd() : line));
        if (inPlace)
        {
            // One newline, the last thing written.
            Assert.EndsWith(Clear + Environment.NewLine, text, StringComparison.Ordinal);
            int newline = text.IndexOf(Environment.NewLine, StringComparison.Ordinal);
            Assert.Equal(text.Length - Environment.NewLine.Length, newline);
        }
    }

    private static SyncProgress Progress(SyncPhase phase, int done, int? total) =>
        new(phase, Start, done, total, new SyncCounts(), 0, 0);

    private static string[] Lines(StringWriter output) =>
        output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

    // One progress line and nothing else: time, label, detail, and (in place) the clearing sequence once at its end.
    [GeneratedRegex(@"\A\[\d\d:\d\d\] [a-z ]{13} [^\[\r\n\u001b]+(\u001b\[K)?\z")]
    private static partial Regex WholeLine();
}
