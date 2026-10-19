using System.Collections.Concurrent;
using TonTuyau.Application.Sync;

namespace TonTuyau.Application.UnitTests;

/// <summary>The progress a run reports through its <see cref="RunContext"/>.</summary>
public sealed class RunContextProgressTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Nothing_is_reported_before_the_first_phase()
    {
        Recorder recorder = new();
        RunContext context = Context(recorder);

        context.Count(c => c with { VideosRead = 100 });
        context.CheckedFile("a", exists: false);

        Assert.Empty(recorder.Reports);
    }

    [Fact]
    public void A_phase_is_reported_at_once_then_with_each_step_and_count()
    {
        Recorder recorder = new();
        RunContext context = Context(recorder);

        context.BeginPhase(SyncPhase.ChannelVideos, 120);
        context.Count(c => c with { VideosRead = 100 });
        context.Progressed(1);
        context.BeginPhase(SyncPhase.MediaCheck);

        Assert.Equal(
            [
                new SyncProgress(SyncPhase.ChannelVideos, Start, 0, 120, new SyncCounts(), 0, 0),
                new SyncProgress(SyncPhase.ChannelVideos, Start, 0, 120, new SyncCounts { VideosRead = 100 }, 0, 0),
                new SyncProgress(SyncPhase.ChannelVideos, Start, 1, 120, new SyncCounts { VideosRead = 100 }, 0, 0),
                new SyncProgress(SyncPhase.MediaCheck, Start, 0, null, new SyncCounts { VideosRead = 100 }, 0, 0),
            ],
            recorder.Reports);
    }

    [Fact]
    public void Files_checked_and_missing_are_counted()
    {
        Recorder recorder = new();
        RunContext context = Context(recorder);

        context.CheckedFile("a", exists: true);
        context.CheckedFile("b", exists: false);
        context.CheckedFile("c", exists: true);
        context.BeginPhase(SyncPhase.MediaCheck);

        SyncProgress last = recorder.Reports[^1];
        Assert.Equal((3, 1), (last.FilesChecked, last.FilesMissing));
        Assert.Equal(["b"], context.MissingMedia);
    }

    // The art resizing counts from several threads at once: no count may be lost, and the reports must arrive in
    // order (each one at least as far as the one before), or the CLI could show a count going backwards.
    [Fact]
    public async Task Counts_from_many_threads_are_all_kept_and_reported_in_order()
    {
        const int Threads = 8;
        const int PerThread = 2_000;
        Recorder recorder = new();
        RunContext context = Context(recorder);
        context.BeginPhase(SyncPhase.ChannelVideos, 1);

        using Barrier barrier = new(Threads);
        Task[] workers = Enumerable.Range(0, Threads)
            .Select(_ => Task.Run(
                () =>
                {
                    barrier.SignalAndWait(TestContext.Current.CancellationToken);
                    for (int i = 0; i < PerThread; i++)
                    {
                        context.Count(c => c with { ArtResized = c.ArtResized + 1, ArtCopied = c.ArtCopied + 2 });
                    }
                },
                TestContext.Current.CancellationToken))
            .ToArray();
        await Task.WhenAll(workers);

        SyncCounts counts = context.Run.Counts;
        Assert.Equal((Threads * PerThread, Threads * PerThread * 2), (counts.ArtResized, counts.ArtCopied));
        int[] reported = recorder.Reports
            .Select(r => r.Counts.ArtResized)
            .ToArray();
        Assert.Equal(Enumerable.Range(0, (Threads * PerThread) + 1), reported);
        Assert.All(recorder.Reports, r => Assert.Equal(r.Counts.ArtResized * 2, r.Counts.ArtCopied));
    }

    private static RunContext Context(ISyncProgressReporter reporter) =>
        new(
            state: null!, // nothing is saved in these tests
            new SyncRun { Id = "run-test", Mode = SyncMode.Import, Trigger = SyncTrigger.Cli, StartedAt = Start },
            new HashSet<string>(),
            reporter,
            Start,
            CancellationToken.None);

    private sealed class Recorder : ISyncProgressReporter
    {
        private readonly ConcurrentQueue<SyncProgress> _reports = new();

        public IReadOnlyList<SyncProgress> Reports => [.. _reports];

        public void Report(SyncProgress progress) => _reports.Enqueue(progress);
    }
}
