using System.Text.Json;
using System.Text.Json.Serialization;
using TonTuyau.Application.Sync;

namespace TonTuyau.Infrastructure.Runs;

/// <summary>How a run is written in <c>runs.run</c>: camelCase, its kinds and outcomes by name.</summary>
internal static class RunJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Write(SyncRun run) => JsonSerializer.Serialize(run, Options);

    public static SyncRun Read(string json) =>
        JsonSerializer.Deserialize<SyncRun>(json, Options)
            ?? throw new InvalidOperationException("A run's JSON is null.");

    public static RunRow ToRow(SyncRun run) => new()
    {
        Id = run.Id,
        Mode = run.Mode,
        Trigger = run.Trigger,
        Outcome = run.Outcome,
        // PostgreSQL takes instants in UTC only.
        StartedAt = run.StartedAt.ToUniversalTime(),
        FinishedAt = run.FinishedAt?.ToUniversalTime(),
        Run = Write(run),
    };
}
