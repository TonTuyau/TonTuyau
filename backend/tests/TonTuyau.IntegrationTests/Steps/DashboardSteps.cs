using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TonTuyau.Application.Dashboard;
using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;
using TonTuyau.Infrastructure.Elasticsearch;
using TonTuyau.IntegrationTests.Support;

namespace TonTuyau.IntegrationTests.Steps;

/// <summary>The admin dashboard: run history written straight to the store, and the answer's tables checked.</summary>
[Binding]
public sealed class DashboardSteps(ScenarioState state)
{
    private JsonNode Json => JsonNode.Parse(Encoding.UTF8.GetString(state.ResponseBody))
        ?? throw new InvalidOperationException("The body is JSON null.");

    /// <summary>
    /// Columns: started (ISO instant; each run finishes 10 minutes later unless running), outcome, new (videos
    /// created), videos / shorts / streams (empty: not recorded, as in 1.1), rebuild.
    /// </summary>
    [Given("the TubeArchivist imports:")]
    public async Task TheSyncRuns(DataTable table)
    {
        await state.WaitUntilReadyAsync();
        ISyncStateStore store = state.Backend.Services.GetRequiredService<ISyncStateStore>();
        foreach (DataTableRow row in table.Rows)
        {
            DateTimeOffset started = DateTimeOffset.Parse(row["started"], CultureInfo.InvariantCulture);
            SyncOutcome outcome = Enum.Parse<SyncOutcome>(row["outcome"], ignoreCase: true);
            TypeCounts? byType = row["videos"] == ""
                ? null
                : new TypeCounts(Number(row["videos"]), Number(row["shorts"]), Number(row["streams"]));
            SyncRun run = new()
            {
                Id = SyncRun.NewId(started),
                Mode = SyncMode.Import,
                Trigger = SyncTrigger.Startup,
                StartedAt = started,
                FinishedAt = outcome == SyncOutcome.Running ? null : started.AddMinutes(10),
                Outcome = outcome,
                IndexRebuild = bool.Parse(row["rebuild"]),
                Counts = new SyncCounts { Created = (int)Number(row["new"]), CreatedByType = byType },
                Notes = ["A note the dashboard doesn't read."],
            };
            await store.SaveRunAsync(run, CancellationToken.None);
        }
    }

    /// <summary>A refresh on YouTube (1.5): never an import for the dashboard.</summary>
    [Given("a refresh ran at {string}")]
    public async Task ARefreshRan(string at)
    {
        await state.WaitUntilReadyAsync();
        DateTimeOffset started = DateTimeOffset.Parse(at, CultureInfo.InvariantCulture);
        SyncRun run = new()
        {
            Id = SyncRun.NewId(started),
            Mode = SyncMode.RefreshChannelVideos,
            Trigger = SyncTrigger.Cli,
            StartedAt = started,
            FinishedAt = started.AddMinutes(2),
            Outcome = SyncOutcome.Succeeded,
            Target = "UCaaaaaaaaaaaaaaaaaaaaaa",
            Counts = new SyncCounts { Created = 140, VideosRead = 140 },
        };
        await state.Backend.Services
            .GetRequiredService<ISyncStateStore>()
            .SaveRunAsync(run, CancellationToken.None);
    }

    [Then("the play time by type is:")]
    public void PlayTimeIs(DataTable table) =>
        AssertRows(table, Json["playTime"]!["types"]!, (item, column) => Scalar(item[column]));

    [Then("the dashboard's channels are:")]
    public void ChannelsAre(DataTable table) =>
        AssertRows(
            table,
            Json["channels"]!,
            (item, column) => column switch
            {
                "videos" or "shorts" or "streams" => Scalar(item["counts"]![column]),
                _ => Scalar(item[column]),
            });

    /// <summary>Restarts the backend (log in after this) with a repository that pages through the runs.</summary>
    [Given("the dashboard reads the runs {int} at a time")]
    public void RunsPerPage(int runs)
    {
        state.ServiceOverrides.Add(services => services.Replace(ServiceDescriptor.Singleton<IDashboardRepository>(
            sp => new DashboardRepository(
                sp.GetRequiredService<ElasticsearchClient>(),
                sp.GetRequiredService<IndexNames>(),
                runs))));
        state.StopBackend();
    }

    [Then("the import history is by {string} in {string}: {int} bars, {int} new videos")]
    public void ImportHistoryIs(string step, string timeZone, int bars, long total)
    {
        JsonNode history = Json;
        Assert.Equal(
            (step, timeZone, bars, total),
            (
                history["step"]!.GetValue<string>(),
                history["timeZone"]!.GetValue<string>(),
                history["bars"]!.AsArray().Count,
                history["total"]!.GetValue<long>()));
    }

    /// <summary>The bars whose total isn't 0, oldest first.</summary>
    [Then("its bars with new videos are:")]
    public void BarsWithNewVideos(DataTable table)
    {
        JsonArray bars = [.. Json["bars"]!
            .AsArray()
            .Where(b => b!["total"]!.GetValue<long>() > 0)
            .Select(b => b!.DeepClone())];
        AssertRows(
            table,
            bars,
            (item, column) => column switch
            {
                "videos" or "shorts" or "streams" => Scalar(item["byType"]![column]),
                _ => Scalar(item[column]),
            });
    }

    private static void AssertRows(DataTable table, JsonNode items, Func<JsonNode, string, string> value)
    {
        string[] columns = [.. table.Header];
        string[] expected = table.Rows
            .Select(r => string.Join(" | ", columns.Select(c => TestIds.Expand(r[c]))))
            .ToArray();
        string[] actual = items
            .AsArray()
            .Select(i => string.Join(" | ", columns.Select(c => value(i!, c))))
            .ToArray();
        Assert.Equal(expected, actual);
    }

    private static string Scalar(JsonNode? node) => node switch
    {
        null => "null",
        JsonValue v when v.TryGetValue(out string? text) => text,
        _ => node.ToJsonString(),
    };

    private static long Number(string text) => long.Parse(text, CultureInfo.InvariantCulture);
}
