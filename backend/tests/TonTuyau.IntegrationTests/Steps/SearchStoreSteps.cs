using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.Configuration;
using TonTuyau.Application.Library;
using TonTuyau.Application.Store;
using TonTuyau.Application.YouTube;
using TonTuyau.Infrastructure.Elasticsearch;
using TonTuyau.IntegrationTests.Support;

namespace TonTuyau.IntegrationTests.Steps;

[Binding]
public sealed class SearchStoreSteps(ScenarioState state)
{
    /// <summary>The videos' channel: in the library before them.</summary>
    private static readonly Channel Channel = new(
        "UCaaaaaaaaaaaaaaaaaaaaaa",
        "Chaîne",
        "",
        0,
        [],
        null,
        null,
        null,
        YouTubeStatus.NotChecked);

    private readonly Dictionary<string, Video> _videos = new(StringComparer.Ordinal);
    private UpsertResult? _upsert;
    private int _deleted;
    private IndexRebuild? _rebuild;

    private IServiceProvider Services => state.Backend.Services;

    private IVideoRepository Videos => Services.GetRequiredService<IVideoRepository>();

    private IIndexManager Indices => Services.GetRequiredService<IIndexManager>();

    private string Prefix => state.Settings.ToConfiguration()[ConfigKeys.EsIndexPrefix]!;

    [Given("the backend is ready")]
    [Then("the backend becomes ready")]
    public async Task BackendIsReady()
    {
        using HttpClient client = state.Backend.CreateClient();
        DateTime deadline = DateTime.UtcNow.AddSeconds(60);
        while (true)
        {
            using HttpResponseMessage response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));
            if (response.StatusCode == HttpStatusCode.OK)
            {
                return;
            }

            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail($"Not ready after 60 s: {await response.Content.ReadAsStringAsync()}");
            }

            await Task.Delay(200);
        }
    }

    [When("the backend restarts")]
    public void BackendRestarts() => state.StopBackend();

    [Given("the videos {string} are stored")]
    [When("the videos {string} are stored")]
    public async Task VideosAreStored(string ids)
    {
        // Their channel first, as the import adds it.
        await Services
            .GetRequiredService<IChannelRepository>()
            .AddMissingAsync([Channel], CancellationToken.None);
        await Videos.AddMissingAsync(VideosFor(ids), CancellationToken.None);
        await Indices.RefreshAsync(CancellationToken.None);
    }

    [Given("the videos {string} are indexed")]
    [When("the videos {string} are indexed")]
    public async Task VideosAreIndexed(string ids) => await IndexAsync(ids);

    [When("the title of {string} becomes {string} and the videos {string} are indexed")]
    public async Task TitleChangesAndIndex(string id, string title, string ids)
    {
        _videos[id] = VideoFor(id) with { Title = title };
        await IndexAsync(ids);
    }

    [When("the videos {string} are deleted from the index")]
    public async Task VideosAreDeleted(string ids)
    {
        _deleted = await Services
            .GetRequiredService<VideoIndex>()
            .DeleteAsync(Ids(ids), CancellationToken.None);
        await Indices.RefreshAsync(CancellationToken.None);
    }

    [Then("{int} were created, {int} updated and {int} unchanged")]
    public void UpsertCounts(int created, int updated, int unchanged) =>
        Assert.Equal((created, updated, unchanged), (_upsert!.Created, _upsert.Updated, _upsert.Unchanged));

    [Then("the index's video {string} has the title {string}")]
    public async Task VideoHasTitle(string id, string title)
    {
        StoredVideo? indexed = await Services
            .GetRequiredService<VideoIndex>()
            .GetStoredAsync(id, CancellationToken.None);
        Assert.Equal(title, indexed?.Video.Title);
    }

    [Then("{int} video(s) was/were deleted")]
    public void DeletedCount(int count) => Assert.Equal(count, _deleted);

    [Then("the store holds {int} video(s)")]
    public async Task StoreHolds(int count) => Assert.Equal(count, await Videos.CountAsync(CancellationToken.None));

    [Given("a rebuild has started")]
    [When("a rebuild starts")]
    public async Task RebuildStarts() => _rebuild = await Indices.BeginRebuildAsync(CancellationToken.None);

    [When("another rebuild starts")]
    public async Task AnotherRebuildStarts()
    {
        try
        {
            await Indices.BeginRebuildAsync(CancellationToken.None);
        }
        catch (SearchStoreException ex)
        {
            state.Error = ex;
        }
    }

    [When("the rebuild completes")]
    public async Task RebuildCompletes()
    {
        await Indices.CompleteRebuildAsync(_rebuild!, CancellationToken.None);
        await Indices.RefreshAsync(CancellationToken.None);
    }

    [When("the rebuild is aborted")]
    public async Task RebuildAborted()
    {
        await Indices.AbortRebuildAsync(_rebuild!, CancellationToken.None);
        await Indices.RefreshAsync(CancellationToken.None);
    }

    [Then("it is refused because a rebuild is already in progress")]
    public void RebuildRefused() =>
        Assert.Contains(
            "already in progress",
            Assert.IsType<SearchStoreException>(state.Error).Message,
            StringComparison.Ordinal);

    /// <summary>What users browse: the index's read alias (PostgreSQL holds both, since 2.0).</summary>
    [Then("reads find {string} but not {string}")]
    public async Task ReadsFind(string present, string absent)
    {
        VideoIndex index = Services.GetRequiredService<VideoIndex>();
        Assert.NotNull(await index.GetStoredAsync(present, CancellationToken.None));
        Assert.Null(await index.GetStoredAsync(absent, CancellationToken.None));
    }

    [Then("reads and writes of {string} both use generation {int}")]
    public async Task Generation(string index, int version)
    {
        IndexStatus status = (await Indices.GetStatusAsync(CancellationToken.None)).Single(
            s => s.Index == Parse(index));
        Assert.Equal($"{Prefix}-{index}-v{version}", status.ReadIndex);
        Assert.Equal(status.ReadIndex, status.WriteIndex);
        JsonNode generations = await RawAsync(HttpMethod.Get, $"/{Prefix}-{index}-v*");
        string[] leftovers = generations
            .AsObject()
            .Select(kv => kv.Key)
            .ToArray();
        Assert.Equal([status.ReadIndex], leftovers);
    }

    [Given("a leftover generation {int} of every index, without aliases, holds the video {string}")]
    public async Task LeftoverGeneration(int version, string id)
    {
        foreach (string index in (string[])["videos", "channels"])
        {
            JsonObject definition = Infrastructure.Elasticsearch.IndexDefinitions.For(Parse(index));
            await RawAsync(HttpMethod.Put, $"/{Prefix}-{index}-v{version}", definition.ToJsonString());
        }

        JsonObject document = new() { ["id"] = id, ["title"] = "Leftover" };
        await RawAsync(HttpMethod.Put, $"/{Prefix}-videos-v{version}/_doc/{id}?refresh=true", document.ToJsonString());
    }

    [Then("writes of {string} go to generation {int} and reads to generation {int}")]
    public async Task WritesAndReads(string index, int write, int read)
    {
        IndexStatus status = (await Indices.GetStatusAsync(CancellationToken.None)).Single(
            s => s.Index == Parse(index));
        Assert.Equal($"{Prefix}-{index}-v{write}", status.WriteIndex);
        Assert.Equal($"{Prefix}-{index}-v{read}", status.ReadIndex);
    }

    [Then("generation {int} of {string} doesn't hold the video {string}")]
    public async Task GenerationDoesNotHold(int version, string index, string id)
    {
        JsonNode count = await RawAsync(HttpMethod.Get, $"/{Prefix}-{index}-v{version}/_count?q=id:{id}");
        Assert.Equal(0, count["count"]!.GetValue<long>());
    }

    [Given("the {string} index already exists with an outdated definition")]
    public async Task OutdatedIndex(string index)
    {
        // What an older version would have left behind: same index and aliases, a different definition hash.
        string name = $"{Prefix}-{index}-v1";
        JsonObject definition = Infrastructure.Elasticsearch.IndexDefinitions.For(Parse(index));
        definition["mappings"]!["_meta"]!["definition_hash"] = "older-version";
        definition["aliases"] = new JsonObject
        {
            [$"{Prefix}-{index}"] = new JsonObject(),
            [$"{Prefix}-{index}-write"] = new JsonObject()
        };
        await RawAsync(HttpMethod.Put, $"/{name}", definition.ToJsonString());
    }

    /// <summary>An upgrade whose index definition changed: the live generation carries the older hash (QA10 M5-10).</summary>
    [Given("the live {string} index was made by an older version")]
    public async Task LiveIndexOutdated(string index)
    {
        JsonObject meta = new()
        {
            ["_meta"] = new JsonObject { ["tontuyau_index"] = index, ["definition_hash"] = "older-version" },
        };
        await RawAsync(HttpMethod.Put, $"/{Prefix}-{index}/_mapping", meta.ToJsonString());
    }

    [Then("the {string} index is reported as outdated")]
    public async Task ReportedOutdated(string index) =>
        Assert.False((await Indices.GetStatusAsync(CancellationToken.None)).Single(s => s.Index == Parse(index))
            .DefinitionUpToDate);

    [Then("the {string} index is reported as up to date")]
    public async Task ReportedUpToDate(string index) =>
        Assert.True((await Indices.GetStatusAsync(CancellationToken.None)).Single(s => s.Index == Parse(index))
            .DefinitionUpToDate);

    [Then("the French analysis of {string} and {string} gives the same terms")]
    public async Task FrenchSameTerms(string written, string searched)
        => await SameTermsAsync("tontuyau_french", written, searched);

    [Then("the English analysis of {string} and {string} gives the same terms")]
    public async Task EnglishSameTerms(string written, string searched)
        => await SameTermsAsync("tontuyau_english", written, searched);

    [Then("{string} answers {int}")]
    public async Task Answers(string path, int status)
    {
        using HttpClient client = state.Backend.CreateClient();
        using HttpResponseMessage response = await client.GetAsync(new Uri(path, UriKind.Relative));
        Assert.Equal((HttpStatusCode)status, response.StatusCode);
    }

    private async Task SameTermsAsync(string analyzer, string written, string searched)
    {
        Assert.Equal(
            await TermsAsync(analyzer, written),
            await TermsAsync(analyzer, searched));
    }

    private async Task<string[]> TermsAsync(string analyzer, string text)
    {
        string body = new JsonObject { ["analyzer"] = analyzer, ["text"] = text }.ToJsonString();
        JsonNode result = await RawAsync(HttpMethod.Post, $"/{Prefix}-videos/_analyze", body);
        return result["tokens"]!
            .AsArray()
            .Select(t => t!["token"]!.GetValue<string>())
            .ToArray();
    }

    /// <summary>Straight to the index (the write alias), as the library writes it after PostgreSQL.</summary>
    private async Task IndexAsync(string ids)
    {
        _upsert = await Services
            .GetRequiredService<VideoIndex>()
            .IndexAsync([.. VideosFor(ids).Select(v => new StoredVideo(v, false))], CancellationToken.None);
        await Indices.RefreshAsync(CancellationToken.None);
    }

    private Video[] VideosFor(string ids) =>
        Ids(ids)
            .Select(VideoFor)
            .ToArray();

    private Video VideoFor(string id)
    {
        if (!_videos.TryGetValue(id, out Video? video))
        {
            video = new Video(
                id,
                "UCaaaaaaaaaaaaaaaaaaaaaa",
                "Chaîne",
                $"Vidéo {id}",
                "",
                VideoType.Video,
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.UnixEpoch,
                TimeSpan.FromSeconds(60),
                1000,
                $"UCaaaaaaaaaaaaaaaaaaaaaa/{id}.mp4",
                false,
                $"/cache/videos/{id[0]}/{id}.jpg",
                [],
                [],
                new VideoStats(0, 0, 0),
                [],
                YouTubeStatus.NotChecked);
            _videos[id] = video;
        }

        return video;
    }

    private static string[] Ids(string list)
        => list.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static StoreIndex Parse(string index) => index switch
    {
        "videos" => StoreIndex.Videos,
        "channels" => StoreIndex.Channels,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    private async Task<JsonNode> RawAsync(HttpMethod method, string path, string? body = null)
    {
        using HttpClient http = new() { BaseAddress = new Uri(state.Settings.ToConfiguration()[ConfigKeys.EsUrl]!) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"elastic:{ElasticsearchContainer.Password}")));
        using HttpRequestMessage request = new(method, path);
        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        using HttpResponseMessage response = await http.SendAsync(request);
        string text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{method} {path}: {(int)response.StatusCode} {text}");
        return JsonNode.Parse(text)!;
    }
}
