using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using TonTuyau.Application.Store;

namespace TonTuyau.Infrastructure.Elasticsearch;

/// <summary>
/// Index settings + mappings from the embedded <c>Definitions/*.json</c>. Each definition carries its own hash in
/// <c>_meta.definition_hash</c>, so an index created by an older TonTuyau can be detected (→ rebuild).
/// </summary>
internal static class IndexDefinitions
{
    /// <summary>The start of every analyser, filter and normaliser name in <c>Definitions/analysis.json</c>.</summary>
    public const string AnalysisPrefix = "tontuyau_";

    public static JsonObject For(StoreIndex index)
    {
        JsonObject definition = Load(IndexNames.Name(index));
        JsonObject settings = definition["settings"]!.AsObject();
        if (settings["analysis"]?.GetValue<string>() == "@analysis")
        {
            settings["analysis"] = Load("analysis");
        }

        JsonObject mappings = definition["mappings"]!.AsObject();
        mappings["_meta"] = new JsonObject
        {
            ["tontuyau_index"] = IndexNames.Name(index),
            ["definition_hash"] = HashOf(definition),
        };
        return definition;
    }

    /// <summary>The embedded <c>Definitions/&lt;name&gt;.json</c> as is (plain indices: sync, sync-runs).</summary>
    public static string Raw(string name) => Load(name).ToJsonString();

    public static string HashOf(StoreIndex index)
        => For(index)["mappings"]!["_meta"]!["definition_hash"]!.GetValue<string>();

    private static string HashOf(JsonObject definitionWithoutMeta)
    {
        string json = definitionWithoutMeta.ToJsonString();
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)))[..16];
    }

    private static JsonObject Load(string name)
    {
        Assembly assembly = typeof(IndexDefinitions).Assembly;
        string resource = assembly.GetManifestResourceNames()
            .Single(n => n.EndsWith($".Definitions.{name}.json", StringComparison.Ordinal));
        using Stream stream = assembly.GetManifestResourceStream(resource)!;
        return JsonNode.Parse(stream)!.AsObject();
    }
}
