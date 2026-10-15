using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using TonTuyau.Application.Configuration;
using TonTuyau.Application.Sync;
using TonTuyau.Infrastructure.Elasticsearch;
using TonTuyau.Infrastructure.Elasticsearch.Documents;

namespace TonTuyau.Infrastructure.UnitTests;

public sealed class EsClientTests
{
    private static readonly ElasticsearchClient Client =
        EsClient.Create(new ElasticsearchOptions("http://es.test:9200", "elastic", "password"));

    /// <summary>The exact strings the term queries use (keyword fields are case-sensitive).</summary>
    private static readonly Dictionary<SyncMode, string> Modes = new()
    {
        [SyncMode.Incremental] = "incremental",
        [SyncMode.Full] = "full",
        [SyncMode.ApplyDeletions] = "apply_deletions",
        [SyncMode.RefreshVideo] = "refresh_video",
        [SyncMode.RefreshChannel] = "refresh_channel",
        [SyncMode.RefreshChannelVideos] = "refresh_channel_videos",
    };

    private static readonly Dictionary<SyncOutcome, string> Outcomes = new()
    {
        [SyncOutcome.Running] = "running",
        [SyncOutcome.Succeeded] = "succeeded",
        [SyncOutcome.Failed] = "failed",
        [SyncOutcome.Canceled] = "canceled",
    };

    [Theory, CombinatorialData]
    public void Enums_are_stored_as_snake_case_values(SyncMode mode, SyncOutcome outcome)
    {
        SyncRun run = new()
        {
            Id = "run-x",
            Mode = mode,
            Trigger = SyncTrigger.Cli,
            StartedAt = DateTimeOffset.UnixEpoch,
            Outcome = outcome,
        };

        string json = Serialize(run);

        Assert.Contains($"\"mode\":\"{Modes[mode]}\"", json, StringComparison.Ordinal);
        Assert.Contains($"\"outcome\":\"{Outcomes[outcome]}\"", json, StringComparison.Ordinal);
        Assert.Contains("\"started_at\":", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Field_names_inferred_from_expressions_match_the_mappings()
    {
        Assert.Equal("channel_id", Client.Infer.Field(Infer.Field<VideoDocument>(v => v.ChannelId)));
        Assert.Equal("seen_in_run", Client.Infer.Field(Infer.Field<VideoDocument>(v => v.SeenInRun!)));
        Assert.Equal("media_missing", Client.Infer.Field(Infer.Field<VideoDocument>(v => v.MediaMissing!)));
        Assert.Equal("started_at", Client.Infer.Field(Infer.Field<SyncRun>(r => r.StartedAt)));
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
