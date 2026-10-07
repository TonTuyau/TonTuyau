using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TonTuyau.Application.Configuration;
using TonTuyau.Application.Sync;
using TonTuyau.IntegrationTests.Support;

namespace TonTuyau.IntegrationTests.Steps;

[Binding]
public sealed class SyncStateSteps(ScenarioState state) : IAsyncDisposable
{
    private ISyncLease? _first;
    private ISyncLease? _second;
    private bool _tried;

    private ISyncStateStore Store => state.Backend.Services.GetRequiredService<ISyncStateStore>();

    private string Prefix => state.Settings.ToConfiguration()[ConfigKeys.EsIndexPrefix]!;

    [Given("leases last {int} seconds and are renewed every {int} ms")]
    public void LeaseTiming(int leaseSeconds, int renewMs)
    {
        SyncLockSettings timing = new(TimeSpan.FromSeconds(leaseSeconds), TimeSpan.FromMilliseconds(renewMs));
        state.ServiceOverrides.Add(services => services.Replace(ServiceDescriptor.Singleton(timing)));

        // The Background already started a backend: restart it so the new timing applies.
        state.StopBackend();
        Assert.Equal(timing, state.Backend.Services.GetRequiredService<SyncLockSettings>());
    }

    [Given("a sync holds the lock")]
    public async Task ASyncHoldsTheLock()
    {
        _first = await Store.TryAcquireLockAsync(SyncMode.Full, SyncTrigger.Scheduler, CancellationToken.None);
        Assert.NotNull(_first);
    }

    [Given("a sync crashed while holding the lock, and its lease expired a minute ago")]
    public Task CrashedExpired() => WriteLockAsync(DateTimeOffset.UtcNow.AddMinutes(-1));

    [Given("a sync crashed while holding the lock, and its lease runs for another {int} minutes")]
    public Task CrashedRunning(int minutes) => WriteLockAsync(DateTimeOffset.UtcNow.AddMinutes(minutes));

    [When("another sync tries to take the lock")]
    public async Task AnotherSyncTries()
    {
        _second = await Store.TryAcquireLockAsync(SyncMode.Incremental, SyncTrigger.Cli, CancellationToken.None);
        _tried = true;
    }

    [When("the first sync releases the lock")]
    public async Task FirstReleases()
    {
        await _first!.DisposeAsync();
        _first = null;
    }

    [When("{int} seconds pass")]
    public static Task SecondsPass(int seconds) => Task.Delay(TimeSpan.FromSeconds(seconds));

    [When("another process overwrites the lock")]
    public Task Overwrite() => WriteLockAsync(DateTimeOffset.UtcNow.AddMinutes(10));

    [Then("it gets the lock")]
    public void GetsIt()
    {
        Assert.True(_tried);
        Assert.NotNull(_second);
    }

    [Then("it doesn't get the lock")]
    public void DoesNotGetIt()
    {
        Assert.True(_tried);
        Assert.Null(_second);
    }

    [Then("the lock still belongs to the first sync")]
    public async Task StillFirst()
    {
        SyncLockInfo? holder = await Store.GetLockAsync(CancellationToken.None);
        Assert.Equal(_first!.Info.Owner, holder?.Owner);
        Assert.True(holder!.LeaseUntil > DateTimeOffset.UtcNow);
        Assert.False(_first.Lost.IsCancellationRequested);
    }

    [Then("another sync still can't take the lock")]
    public async Task StillCannot()
    {
        await AnotherSyncTries();
        DoesNotGetIt();
    }

    [Then("the first sync is told within {int} seconds that it lost the lock")]
    public async Task ToldLost(int seconds)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (!_first!.Lost.IsCancellationRequested && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        Assert.True(_first.Lost.IsCancellationRequested);
    }

    [When("the checkpoint is saved with watermark {long} and a full run requested")]
    public Task SaveCheckpoint(long watermark) =>
        Store.SaveCheckpointAsync(new SyncCheckpoint(watermark, true), CancellationToken.None);

    [Then("the checkpoint has watermark {long} and a full run requested")]
    public async Task CheckpointIs(long watermark) =>
        Assert.Equal(new SyncCheckpoint(watermark, true), await Store.GetCheckpointAsync(CancellationToken.None));

    [When("these runs are saved:")]
    public async Task SaveRuns(DataTable table)
    {
        foreach (DataTableRow? row in table.Rows)
        {
            SyncRun run = new()
            {
                Id = row["id"],
                Mode = Enum.Parse<SyncMode>(row["mode"], ignoreCase: true),
                Trigger = SyncTrigger.Scheduler,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(
                    -int.Parse(row["started minutes ago"], System.Globalization.CultureInfo.InvariantCulture)),
                Outcome = Enum.Parse<SyncOutcome>(row["outcome"], ignoreCase: true),
            };
            await Store.SaveRunAsync(run, CancellationToken.None);
        }
    }

    [Then("the recent runs are {string}")]
    public async Task RecentRuns(string ids)
    {
        IReadOnlyList<SyncRun> runs = await Store.GetRecentRunsAsync(10, CancellationToken.None);
        Assert.Equal(ids, string.Join(", ", runs.Select(r => r.Id)));
    }

    [Then("the latest finished full run is {string}")]
    public async Task LatestFull(string id) =>
        Assert.Equal(id, (await Store.GetLatestFullRunAsync(CancellationToken.None))?.Id);

    [When("the art {string} is pending")]
    public Task ArtPending(string paths) =>
        Store.SavePendingArtAsync(Split(paths).ToHashSet(StringComparer.Ordinal), CancellationToken.None);

    [Then("the pending art is {string}")]
    public async Task PendingArtIs(string paths)
    {
        IReadOnlySet<string> pending = await Store.GetPendingArtAsync(CancellationToken.None);
        Assert.Equal(Split(paths), pending.Order(StringComparer.Ordinal));
    }

    public async ValueTask DisposeAsync()
    {
        if (_first is not null)
        {
            await _first.DisposeAsync();
        }

        if (_second is not null)
        {
            await _second.DisposeAsync();
        }
    }

    private static string[] Split(string list) =>
        list.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Writes the lock document directly, as another (or a crashed) process would have left it.</summary>
    private async Task WriteLockAsync(DateTimeOffset leaseUntil)
    {
        JsonObject body = new()
        {
            ["kind"] = "lock",
            ["owner"] = "crashed-host/4242/deadbeef",
            ["mode"] = "full",
            ["trigger"] = "scheduler",
            ["acquired_at"] = leaseUntil.AddMinutes(-10).ToString("O"),
            ["lease_until"] = leaseUntil.ToString("O"),
        };
        using HttpClient http = new() { BaseAddress = new Uri(state.Settings.ToConfiguration()[ConfigKeys.EsUrl]!) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"elastic:{ElasticsearchContainer.Password}")));
        using StringContent content = new(body.ToJsonString(), Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await http.PutAsync(
            new Uri($"/{Prefix}-sync/_doc/lock?refresh=true", UriKind.Relative),
            content);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }
}
