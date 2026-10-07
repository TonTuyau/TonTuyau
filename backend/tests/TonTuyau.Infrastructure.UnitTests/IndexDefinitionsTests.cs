using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using TonTuyau.Application.Store;
using TonTuyau.Infrastructure.Elasticsearch;
using TonTuyau.Infrastructure.Elasticsearch.Documents;

namespace TonTuyau.Infrastructure.UnitTests;

public sealed class IndexDefinitionsTests
{
    [Theory, CombinatorialData]
    public void Definitions_are_strict_and_carry_their_hash(StoreIndex index)
    {
        JsonObject definition = IndexDefinitions.For(index);

        Assert.Equal("strict", definition["mappings"]!["dynamic"]!.GetValue<string>());
        Assert.Equal(
            IndexDefinitions.HashOf(index),
            definition["mappings"]!["_meta"]!["definition_hash"]!.GetValue<string>());
        Assert.Equal(0, definition["settings"]!["number_of_replicas"]!.GetValue<int>());
    }

    [Theory, CombinatorialData]
    public void Every_analysis_name_starts_with_the_prefix(StoreIndex index)
    {
        JsonObject analysis = IndexDefinitions.For(index)["settings"]!["analysis"]!.AsObject();
        List<string> names = analysis
            .SelectMany(group => group.Value!.AsObject())
            .Select(kv => kv.Key)
            .ToList();

        Assert.NotEmpty(names);
        Assert.All(names, name => Assert.StartsWith(IndexDefinitions.AnalysisPrefix, name, StringComparison.Ordinal));
    }

    [Theory, CombinatorialData]
    public void Analysis_is_inlined(StoreIndex index)
    {
        JsonObject analysis = IndexDefinitions.For(index)["settings"]!["analysis"]!.AsObject();

        Assert.Contains("tontuyau_french", analysis["analyzer"]!.AsObject().Select(kv => kv.Key));
    }

    [Theory, CombinatorialData]
    public void Every_analyzer_used_by_a_field_is_defined(StoreIndex index)
    {
        JsonObject definition = IndexDefinitions.For(index);
        JsonNode analysis = definition["settings"]!["analysis"]!;
        HashSet<string> defined = analysis["analyzer"]!
            .AsObject()
            .Select(kv => kv.Key)
            .ToHashSet();
        HashSet<string> normalizers = analysis["normalizer"]!
            .AsObject()
            .Select(kv => kv.Key)
            .ToHashSet();

        foreach (JsonObject field in Walk(definition["mappings"]!["properties"]!.AsObject()))
        {
            foreach (string key in (string[])["analyzer", "search_analyzer"])
            {
                if (field[key] is { } analyzer)
                {
                    Assert.Contains(analyzer.GetValue<string>(), defined);
                }
            }

            if (field["normalizer"] is { } normalizer)
            {
                Assert.Contains(normalizer.GetValue<string>(), normalizers);
            }
        }
    }

    [Theory]
    [InlineData(StoreIndex.Videos)]
    [InlineData(StoreIndex.Channels)]
    public void Descriptions_and_tags_index_their_word_starts_for_the_word_being_typed(StoreIndex index)
    {
        // QA11 M5-13: start-of-word matches in long text are answered from indexed prefixes, not by scanning terms.
        JsonObject properties = IndexDefinitions.For(index)["mappings"]!["properties"]!.AsObject();
        JsonNode prefix = properties["description"]!["fields"]!["prefix"]!;
        JsonNode tags = properties["tags"]!;

        Assert.Equal("tontuyau_folded", prefix["analyzer"]!.GetValue<string>());
        Assert.Equal("docs", prefix["index_options"]!.GetValue<string>());
        foreach (JsonNode field in (JsonNode[])[prefix, tags])
        {
            Assert.Equal(1, field["index_prefixes"]!["min_chars"]!.GetValue<int>());
            Assert.Equal(5, field["index_prefixes"]!["max_chars"]!.GetValue<int>());
        }

        Assert.Equal("tontuyau_folded", tags["analyzer"]!.GetValue<string>());
    }

    [Fact]
    public void Hash_is_stable_and_differs_between_indices()
    {
        Assert.Equal(IndexDefinitions.HashOf(StoreIndex.Videos), IndexDefinitions.HashOf(StoreIndex.Videos));
        Assert.NotEqual(IndexDefinitions.HashOf(StoreIndex.Videos), IndexDefinitions.HashOf(StoreIndex.Channels));
    }

    // "dynamic": "strict" rejects any field the mapping doesn't know: document types and mappings must match exactly.
    [Theory]
    [InlineData(StoreIndex.Videos, typeof(VideoDocument))]
    [InlineData(StoreIndex.Channels, typeof(ChannelDocument))]
    public void Mapping_has_exactly_the_document_fields(StoreIndex index, Type document)
    {
        JsonObject mapping = IndexDefinitions.For(index)["mappings"]!["properties"]!.AsObject();

        Assert.Equal(FieldsOf(document), NamesOf(mapping));
        if (document == typeof(VideoDocument))
        {
            Assert.Equal(FieldsOf(typeof(TrackDocument)), NamesOf(mapping["tracks"]!["properties"]!.AsObject()));
        }
    }

    private static IEnumerable<string> FieldsOf(Type type) =>
        type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => JsonNamingPolicy.SnakeCaseLower.ConvertName(p.Name))
            .Order();

    private static IEnumerable<string> NamesOf(JsonObject properties) =>
        properties
            .Select(kv => kv.Key)
            .Order();

    private static IEnumerable<JsonObject> Walk(JsonObject properties)
    {
        foreach ((string _, JsonNode? value) in properties)
        {
            JsonObject field = value!.AsObject();
            yield return field;
            foreach (string nested in (string[])["properties", "fields"])
            {
                if (field[nested] is JsonObject children)
                {
                    foreach (JsonObject child in Walk(children))
                    {
                        yield return child;
                    }
                }
            }
        }
    }
}
