using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using TonTuyau.Application.Configuration;
using TonTuyau.Infrastructure.Elasticsearch;
using TonTuyau.Infrastructure.Elasticsearch.Documents;

namespace TonTuyau.Infrastructure.UnitTests;

public sealed class EsClientTests
{
    private static readonly ElasticsearchClient Client =
        EsClient.Create(new ElasticsearchOptions("http://es.test:9200", "elastic", "password"));

    [Fact]
    public void Field_names_inferred_from_expressions_match_the_mappings()
    {
        Assert.Equal("channel_id", Client.Infer.Field(Infer.Field<VideoDocument>(v => v.ChannelId)));
        Assert.Equal("media_missing", Client.Infer.Field(Infer.Field<VideoDocument>(v => v.MediaMissing!)));
    }

    [Fact]
    public void Documents_are_stored_with_the_same_names_as_the_content_hash_uses()
    {
        VideoDocument document = DocumentMapper.ToDocument(SampleData.Video());

        string json = Serialize(document);

        Assert.Contains("\"thumbnail_path\":", json, StringComparison.Ordinal);
        Assert.Contains("\"media_path_corrected\":", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ThumbnailPath\"", json, StringComparison.Ordinal);
    }

    private static string Serialize<T>(T value)
    {
        using MemoryStream stream = new();
        Client.SourceSerializer.Serialize(value, stream);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}
