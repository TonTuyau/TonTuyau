using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.Sync;
using TonTuyau.IntegrationTests.Support;

namespace TonTuyau.IntegrationTests.Steps;

[Binding]
public sealed class RunnerSteps(ScenarioState state, SyncSteps sync) : IDisposable
{
    private readonly SemaphoreSlim _answer = new(0);
    private StartedRun? _started;
    private SyncRun? _ended;
    private bool? _canceled;

    /// <summary>The backend stopping, in the background (from TubeArchivist's answer); null when it doesn't.</summary>
    private Task? _stopping;

    private SyncRunner Runner => state.Backend.Services.GetRequiredService<SyncRunner>();

    /// <summary>Keeps the run going on, at a known point, until the scenario lets it go on.</summary>
    [Given("TubeArchivist holds its answer to page {int} until told")]
    public void HoldPage(int page) =>
        sync.Fake.BeforeAnswer[Page(page)] = () => _answer.Wait(TimeSpan.FromSeconds(30));

    [Given("the run going on in the backend is canceled by {string} right before TubeArchivist answers page {int}")]
    public void CancelBeforePage(string by, int page)
    {
        SyncRunner runner = Runner;
        sync.Fake.BeforeAnswer[Page(page)] = () => runner.Cancel(runner.RunningId!, by);
    }

    [Given("the backend stops right before TubeArchivist answers page {int}")]
    public void StopBeforePage(int page) =>
        sync.Fake.BeforeAnswer[Page(page)] = () => _stopping = Task.Run(state.StopBackend);

    [When("the backend's runs are stopped")]
    public Task StopRuns() => Runner.StopAsync();

    [When("an import starts in the backend")]
    public async Task Start()
    {
        try
        {
            _started = await Runner.StartImportAsync(SyncTrigger.Cli);
        }
        catch (Exception ex) when (ex is SyncBusyException or OperationCanceledException)
        {
            state.Error = ex;
        }
    }

    [When("TubeArchivist answers")]
    public void Answer() => _answer.Release();

    /// <summary>
    /// Its end, and the backend's stop when the scenario stops it: since the run queue (2.0), a run's end is told as soon
    /// as it's recorded, before the backend has stopped, and the next steps would reach a backend half disposed.
    /// </summary>
    [When("the started run ends")]
    public async Task Ends()
    {
        _ended = await _started!.Finished.WaitAsync(TimeSpan.FromSeconds(60));
        if (_stopping is not null)
        {
            await _stopping.WaitAsync(TimeSpan.FromSeconds(60));
        }
    }

    [When("the run {string} is canceled in the backend")]
    public void CancelById(string runId) => _canceled = Runner.Cancel(runId, "admin");

    /// <summary>Asked for while another run holds the lock (2.0): it waits its turn, first in line.</summary>
    [Then("the started run waits in the queue")]
    public async Task Queued()
    {
        SyncRun? stored = await Store().GetRunAsync(_started!.Id, CancellationToken.None);
        Assert.Equal(SyncOutcome.Queued, stored?.Outcome);
        Assert.Null(Runner.RunningId);
        Assert.False(_started.Finished.IsCompleted);
    }

    [Then("the backend says the started run is going on")]
    public async Task GoingOn()
    {
        Assert.Equal(_started!.Id, Runner.RunningId);
        SyncRun? stored = await Store().GetRunAsync(_started.Id, CancellationToken.None);
        Assert.Equal(SyncOutcome.Running, stored?.Outcome);
    }

    [Then("no run is going on in the backend")]
    public void NothingGoingOn() => Assert.Null(Runner.RunningId);

    [Then("the started run succeeded")]
    public void StartedSucceeded() => Ended(SyncOutcome.Succeeded);

    [Then("the started run was canceled")]
    public void StartedCanceled() => Ended(SyncOutcome.Canceled);

    [Then("the stored started run is canceled")]
    public async Task StoredCanceled()
    {
        SyncRun? stored = await Store().GetRunAsync(_started!.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(SyncOutcome.Canceled, stored.Outcome);
        Assert.NotNull(stored.FinishedAt);
    }

    [Then("the backend says that run isn't going on here")]
    public void NotGoingOnHere() => Assert.False(_canceled);

    public void Dispose()
    {
        // A scenario that failed with TubeArchivist still holding its answer: let the run go.
        _answer.Release(10);
        _answer.Dispose();
    }

    private void Ended(SyncOutcome outcome)
    {
        Assert.NotNull(_ended);
        Assert.Equal(_started!.Id, _ended.Id);
        Assert.Equal(outcome, _ended.Outcome);
    }

    private ISyncStateStore Store() => state.Backend.Services.GetRequiredService<ISyncStateStore>();

    private static string Page(int page) => $"page={page.ToString(CultureInfo.InvariantCulture)}";
}
