using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;
using TonTuyau.Infrastructure.Runs;

namespace TonTuyau.Infrastructure.UnitTests;

public sealed class RunJsonTests
{
    [Fact]
    public void A_run_is_read_back_whole()
    {
        SyncRun run = new()
        {
            Id = "run-20261018T070000Z-abcdef",
            Mode = SyncMode.RefreshChannelVideos,
            Trigger = SyncTrigger.Admin,
            StartedAt = new DateTimeOffset(2026, 10, 18, 7, 0, 0, TimeSpan.Zero),
            StartedBy = "admin",
            FinishedAt = new DateTimeOffset(2026, 10, 18, 7, 5, 0, TimeSpan.Zero),
            Outcome = SyncOutcome.OnHold,
            Target = "UCaaaaaaaaaaaaaaaaaaaaaa",
            ResumeAt = new DateTimeOffset(2026, 10, 18, 8, 0, 0, TimeSpan.Zero),
            Remaining = ["aaaaaaaaaaa", "bbbbbbbbbbb"],
            Counts = new SyncCounts { VideosRead = 3, Created = 2, CreatedByType = new TypeCounts(1, 1, 0) },
            Notes = ["A note."],
            RejectedSamples = ["video x: no title"],
        };

        SyncRun read = RunJson.Read(RunJson.Write(run));

        Assert.Equivalent(run, read, strict: true);
    }

    [Fact]
    public void Kinds_and_outcomes_are_written_by_name()
    {
        SyncRun run = new()
        {
            Id = "run-x",
            Mode = SyncMode.IndexRebuild,
            Trigger = SyncTrigger.Cli,
            StartedAt = DateTimeOffset.UnixEpoch,
            Outcome = SyncOutcome.Succeeded,
        };

        string json = RunJson.Write(run);

        Assert.Contains("\"mode\":\"indexRebuild\"", json, StringComparison.Ordinal);
        Assert.Contains("\"outcome\":\"succeeded\"", json, StringComparison.Ordinal);
    }
}
