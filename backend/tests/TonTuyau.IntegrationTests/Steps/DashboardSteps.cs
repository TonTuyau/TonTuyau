using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;
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
    [Given("the sync runs:")]
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
                Mode = SyncMode.Incremental,
                Trigger = SyncTrigger.Scheduler,
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

    [Then("the dashboard's imports are:")]
    public void ImportsAre(DataTable table) =>
        AssertRows(
            table,
            Json["imports"]!,
            (item, column) => column switch
            {
                "videos" or "shorts" or "streams" => item["byType"] is { } byType ? Scalar(byType[column]) : "",
                _ => Scalar(item[column]),
            });

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
