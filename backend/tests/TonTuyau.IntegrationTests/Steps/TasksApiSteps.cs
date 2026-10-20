using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.Browse;
using TonTuyau.Application.Configuration;
using TonTuyau.Application.Sync;
using TonTuyau.Application.Tasks;
using TonTuyau.IntegrationTests.Support;

namespace TonTuyau.IntegrationTests.Steps;

/// <summary>The Tasks page's API: requests about runs whose ids the scenario doesn't know in advance.</summary>
[Binding]
public sealed class TasksApiSteps(ScenarioState state, AuthSteps auth, ApiSteps api)
{
    private IServiceProvider Services => state.Backend.Services;

    private ISyncStateStore Store => Services.GetRequiredService<ISyncStateStore>();

    /// <summary>A run of TonTuyau 1.3 or older, finished long ago: it has no log.</summary>
    [Given("a run from before 1.4, {string}")]
    public async Task OldRun(string id)
    {
        await state.WaitUntilReadyAsync();
        DateTimeOffset started = DateTimeOffset.ParseExact(
            id[4..20],
            "yyyyMMdd'T'HHmmss'Z'",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal);
        SyncRun run = new()
        {
            Id = id,
            Mode = SyncMode.Import,
            Trigger = SyncTrigger.Cli,
            StartedAt = started,
            FinishedAt = started.AddMinutes(1),
            Outcome = SyncOutcome.Succeeded,
        };
        await Store.SaveRunAsync(run, CancellationToken.None);
    }

    /// <summary>A run that another process (the command line) is doing: its record says it's going on.</summary>
    [Given("a run going on in another process, {string}")]
    public async Task RunElsewhere(string id)
    {
        await OldRun(id);
        SyncRun? run = await Store.GetRunAsync(id, CancellationToken.None);
        SyncRun going = run! with { FinishedAt = null, Outcome = SyncOutcome.Running };
        await Store.SaveRunAsync(going, CancellationToken.None);
    }

    [When("I cancel the latest run with my session")]
    public async Task CancelLatest() =>
        await auth.PostWithSession($"/api/admin/runs/{await LatestRunIdAsync()}/cancel");

    /// <summary>The first run on hold of the Tasks page (1.5), canceled: it won't resume.</summary>
    [When("I cancel the run on hold with my session")]
    public async Task CancelHeld()
    {
        await auth.RequestWithSession("/api/admin/tasks");
        string id = ApiSteps.Scalar(api.At("onHold[0].id"));
        await auth.PostWithSession($"/api/admin/runs/{id}/cancel");
    }

    [When("I put the latest run on hold with my session")]
    public async Task HoldLatest() =>
        await auth.PostWithSession($"/api/admin/runs/{await LatestRunIdAsync()}/hold");

    [When("I resume the latest run with my session")]
    public async Task ResumeLatest() =>
        await auth.PostWithSession($"/api/admin/runs/{await LatestRunIdAsync()}/resume");

    /// <param name="suffix">After the run's path: "" for the run, "/log" for its log.</param>
    [When("I request the latest run's {string} with my session")]
    public async Task RequestLatestRun(string suffix) =>
        await auth.RequestWithSession($"/api/admin/runs/{await LatestRunIdAsync()}{suffix}");

    [When("I request the next page of runs with my session")]
    public Task NextPageOfRuns() =>
        auth.RequestWithSession(WithQuery("before", ApiSteps.Scalar(api.At("nextCursor"))));

    [When("I request the lines after the last one with my session")]
    public Task LinesAfterLast() =>
        auth.RequestWithSession(WithQuery("after", ApiSteps.Scalar(api.At("lines")!.AsArray()[^1]!["id"])));

    [Then("{string} is the latest run's id")]
    public async Task IsLatestRunId(string path) =>
        Assert.Equal(await LatestRunIdAsync(), ApiSteps.Scalar(api.At(path)));

    [Then("the latest run ends {word} within {int} seconds")]
    public async Task LatestEnds(string outcome, int seconds)
    {
        SyncOutcome expected = Enum.Parse<SyncOutcome>(outcome, ignoreCase: true);
        DateTime deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (true)
        {
            IReadOnlyList<SyncRun> runs = await Store.GetRecentRunsAsync(1, CancellationToken.None);
            // Queued ones (2.0) end later: the latest run is the last to start once the queue is empty.
            bool queued = (await state.Backend.Services
                .GetRequiredService<ITaskRepository>()
                .GetQueuedRunsAsync(CancellationToken.None)).Count > 0;
            if (runs[0].Outcome is not (SyncOutcome.Running or SyncOutcome.Queued) && !queued)
            {
                Assert.Equal(expected, runs[0].Outcome);
                return;
            }

            Assert.True(DateTime.UtcNow < deadline, $"{runs[0].Id} still runs after {seconds} s.");
            await Task.Delay(250);
        }
    }

    /// <summary>Waits until the history has that many runs of the task (one a background service asks for).</summary>
    [Then("the history has {int} run(s) of {string} within {int} seconds")]
    public async Task HistoryHasWithin(int count, string task, int seconds)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (await RunsOfAsync(task) != count)
        {
            Assert.True(DateTime.UtcNow < deadline, $"Not {count} runs of {task} after {seconds} s.");
            await Task.Delay(250);
        }
    }

    /// <summary>Checks that the history keeps that many runs of the task for a while (none asked for meanwhile).</summary>
    [Then("the history has {int} run(s) of {string} for {int} seconds")]
    public async Task HistoryKeeps(int count, string task, int seconds)
    {
        DateTime end = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < end)
        {
            Assert.Equal(count, await RunsOfAsync(task));
            await Task.Delay(250);
        }
    }

    /// <summary>Both in the same note.</summary>
    [Then("the latest run's notes mention {string} and {string}")]
    public async Task LatestNotesMentionBoth(string first, string second)
    {
        SyncRun latest = (await Store.GetRecentRunsAsync(1, CancellationToken.None))[0];
        Assert.True(
            latest.Notes.Any(n =>
                n.Contains(first, StringComparison.Ordinal) && n.Contains(second, StringComparison.Ordinal)),
            string.Join(" | ", latest.Notes));
    }

    [Then("the latest run's notes mention {string}")]
    public async Task LatestNotesMention(string text)
    {
        SyncRun latest = (await Store.GetRecentRunsAsync(1, CancellationToken.None))[0];
        Assert.True(
            latest.Notes.Any(n => n.Contains(text, StringComparison.Ordinal)),
            string.Join(" | ", latest.Notes));
    }

    /// <summary>Waits for the latest run to be in this state (a run on hold isn't running, but isn't over either).</summary>
    [Then("the latest run is {word} within {int} seconds")]
    public async Task LatestIs(string outcome, int seconds)
    {
        SyncOutcome expected = Enum.Parse<SyncOutcome>(outcome, ignoreCase: true);
        DateTime deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (true)
        {
            IReadOnlyList<SyncRun> runs = await Store.GetRecentRunsAsync(1, CancellationToken.None);
            if (runs[0].Outcome == expected)
            {
                return;
            }

            Assert.True(DateTime.UtcNow < deadline, $"{runs[0].Id} is {runs[0].Outcome} after {seconds} s.");
            await Task.Delay(250);
        }
    }

    /// <summary>Asks for the latest run until this value of it is the one expected (a run going on, saved as it goes).</summary>
    [Then("the latest run's {string} is {string} within {int} seconds")]
    public async Task LatestRunValueWithin(string path, string expected, int seconds)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (true)
        {
            await RequestLatestRun("");
            string? value = api.At(path) is { } node ? ApiSteps.Scalar(node) : null;
            if (value == expected)
            {
                return;
            }

            Assert.True(DateTime.UtcNow < deadline, $"{path} is {value ?? "missing"} after {seconds} s.");
            await Task.Delay(250);
        }
    }

    [Then("the Location header is the run's address")]
    public void LocationIsRun() =>
        Assert.Equal(
            $"/api/admin/runs/{ApiSteps.Scalar(api.At("id"))}",
            state.Response!.Headers.Location?.OriginalString);

    [Then("the next cursor is the id of the page's last run")]
    public void NextCursorIsLastId() =>
        Assert.Equal(
            ApiSteps.Scalar(api.At("items")!.AsArray()[^1]!["id"]),
            ApiSteps.Scalar(api.At("nextCursor")));

    [Then("{string} mentions {string}")]
    public void Mentions(string path, string text) =>
        Assert.Contains(text, ApiSteps.Scalar(api.At(path)), StringComparison.Ordinal);

    /// <summary>Asks the same thing again until the answer says the log is complete.</summary>
    [Then("the log is complete within {int} seconds")]
    public async Task CompleteWithin(int seconds)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(seconds);
        string path = LastPathAndQuery();
        while (ApiSteps.Scalar(api.At("complete")) != "true")
        {
            Assert.True(DateTime.UtcNow < deadline, $"The log of {path} isn't complete after {seconds} s.");
            await Task.Delay(500);
            await auth.RequestWithSession(path);
        }
    }

    private async Task<string> LatestRunIdAsync()
    {
        IReadOnlyList<SyncRun> runs = await Store.GetRecentRunsAsync(1, CancellationToken.None);
        return runs[0].Id;
    }

    private string LastPathAndQuery() =>
        state.Response?.RequestMessage?.RequestUri?.PathAndQuery
        ?? throw new InvalidOperationException("No request yet.");

    /// <summary>The last request's path, with this query parameter set (added, or its value replaced).</summary>
    private string WithQuery(string name, string value)
    {
        string last = LastPathAndQuery();
        int query = last.IndexOf('?', StringComparison.Ordinal);
        string path = query < 0 ? last : last[..query];
        List<string> parameters = query < 0
            ? []
            : [.. last[(query + 1)..].Split('&').Where(p => !p.StartsWith(name + "=", StringComparison.Ordinal))];
        parameters.Add($"{name}={Uri.EscapeDataString(value)}");
        return $"{path}?{string.Join('&', parameters)}";
    }

    private async Task<long> RunsOfAsync(string task) =>
        (await Services
            .GetRequiredService<ITaskRepository>()
            .GetRunsAsync(ApiValues.Tasks[task], null, 1, CancellationToken.None))
            .Total;
}
