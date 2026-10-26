using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.Browse;
using TonTuyau.Application.Sync;
using TonTuyau.IntegrationTests.Support;

namespace TonTuyau.IntegrationTests.Steps;

/// <summary>
/// The runs of a video or a channel (2.3.0: Run now, the queue going on beside a run held): several at once, so each
/// is named by what it reads rather than as the latest.
/// </summary>
[Binding]
public sealed class RunNowSteps(ScenarioState state, AuthSteps auth, ApiSteps api)
{
    /// <summary>How far back the runs are looked for: a scenario makes a few.</summary>
    private const int RecentRuns = 50;

    private ISyncStateStore Store => state.Backend.Services.GetRequiredService<ISyncStateStore>();

    /// <param name="kind">video | channel.</param>
    [When("I run now the run of the {word} {string} with my session")]
    public async Task RunNow(string kind, string name) =>
        await auth.PostWithSession($"/api/admin/runs/{await RunIdAsync(kind, name)}/run-now");

    [When("I put the run of the {word} {string} on hold with my session")]
    public async Task Hold(string kind, string name) =>
        await auth.PostWithSession($"/api/admin/runs/{await RunIdAsync(kind, name)}/hold");

    [When("I resume the run of the {word} {string} with my session")]
    public async Task Resume(string kind, string name) =>
        await auth.PostWithSession($"/api/admin/runs/{await RunIdAsync(kind, name)}/resume");

    [When("I request the run of the {word} {string} with my session")]
    public async Task Request(string kind, string name) =>
        await auth.RequestWithSession($"/api/admin/runs/{await RunIdAsync(kind, name)}");

    [Then("{string} is the id of the run of the {word} {string}")]
    public async Task IsRunId(string path, string kind, string name) =>
        Assert.Equal(await RunIdAsync(kind, name), ApiSteps.Scalar(api.At(path)));

    /// <summary>Waits for the run to be in this state: running, queued, onHold, or one of the ends.</summary>
    [Then("the run of the {word} {string} is {word} within {int} seconds")]
    public async Task IsWithin(string kind, string name, string outcome, int seconds)
    {
        SyncOutcome expected = Enum.Parse<SyncOutcome>(outcome, ignoreCase: true);
        DateTime deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (true)
        {
            SyncRun run = await RunAsync(kind, name);
            if (run.Outcome == expected)
            {
                return;
            }

            Assert.True(DateTime.UtcNow < deadline, $"{run.Id} is {run.Outcome} after {seconds} s.");
            await Task.Delay(250);
        }
    }

    /// <summary>It stays in this state for a while (the queue doesn't start it).</summary>
    [Then("the run of the {word} {string} stays {word} for {int} seconds")]
    public async Task Stays(string kind, string name, string outcome, int seconds)
    {
        SyncOutcome expected = Enum.Parse<SyncOutcome>(outcome, ignoreCase: true);
        DateTime end = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < end)
        {
            Assert.Equal(expected, (await RunAsync(kind, name)).Outcome);
            await Task.Delay(250);
        }
    }

    /// <summary>Waits for the latest run of this task (one without a target: a sync) to be in this state.</summary>
    [Then("the latest run of {string} is {word} within {int} seconds")]
    public async Task TaskIsWithin(string task, string outcome, int seconds)
    {
        SyncMode mode = ApiValues.Tasks[task];
        SyncOutcome expected = Enum.Parse<SyncOutcome>(outcome, ignoreCase: true);
        DateTime deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (true)
        {
            IReadOnlyList<SyncRun> runs = await Store.GetRecentRunsAsync(RecentRuns, CancellationToken.None);
            SyncRun run = runs.FirstOrDefault(r => r.Mode == mode)
                ?? throw new InvalidOperationException($"No run of {task}.");
            if (run.Outcome == expected)
            {
                return;
            }

            Assert.True(DateTime.UtcNow < deadline, $"{run.Id} is {run.Outcome} after {seconds} s.");
            await Task.Delay(250);
        }
    }

    [Then("the run of the {word} {string} has read {int} videos")]
    public async Task HasRead(string kind, string name, int videos) =>
        Assert.Equal(videos, (await RunAsync(kind, name)).Counts.VideosRead);

    [Then("the run of the {word} {string}'s notes mention {string}")]
    public async Task NotesMention(string kind, string name, string text)
    {
        SyncRun run = await RunAsync(kind, name);
        Assert.True(run.Notes.Any(n => n.Contains(text, StringComparison.Ordinal)), string.Join(" | ", run.Notes));
    }

    private async Task<string> RunIdAsync(string kind, string name) => (await RunAsync(kind, name)).Id;

    /// <summary>The latest run reading this video or channel.</summary>
    private async Task<SyncRun> RunAsync(string kind, string name)
    {
        string target = kind switch
        {
            "video" => FakeTubeArchivist.VideoId(name),
            "channel" => FakeTubeArchivist.ChannelId(name),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "video or channel"),
        };
        IReadOnlyList<SyncRun> runs = await Store.GetRecentRunsAsync(RecentRuns, CancellationToken.None);
        return runs.FirstOrDefault(r => r.Target == target)
            ?? throw new InvalidOperationException($"No run of the {kind} {name}.");
    }
}
