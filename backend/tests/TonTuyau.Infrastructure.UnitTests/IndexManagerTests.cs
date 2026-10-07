using System.Net;
using System.Text;
using Elastic.Transport;
using Microsoft.Extensions.Logging.Abstractions;
using TonTuyau.Application.Configuration;
using TonTuyau.Application.Store;
using TonTuyau.Infrastructure.Elasticsearch;
using HttpMethod = System.Net.Http.HttpMethod;

namespace TonTuyau.Infrastructure.UnitTests;

/// <summary>
/// How the index manager reads Elasticsearch's alias answers (QA14-10), against canned answers: a 404 for an index
/// deleted while the alias was looked up (the race seen in the flaky scenarios) is not a missing alias.
/// </summary>
public sealed class IndexManagerTests
{
    private const string AliasMissing = """{"error":"alias [t-channels] missing","status":404}""";

    /// <summary>What Elasticsearch (security on) answers when another index is deleted during the lookup.</summary>
    private const string IndexDeletedMeanwhile = """
        {"error":{"root_cause":[{"type":"index_not_found_exception","reason":"no such index [other-sync-runs]",
        "index":"other-sync-runs"}],"type":"index_not_found_exception","reason":"no such index [other-sync-runs]",
        "index":"other-sync-runs"},"status":404}
        """;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_index_deleted_during_the_lookup_is_an_error_not_a_missing_alias()
    {
        FakeElasticsearch es = new(request => IsAliasLookup(request, "t-channels")
            ? (HttpStatusCode.NotFound, IndexDeletedMeanwhile)
            : Existing(request));

        SearchStoreException ex = await Assert.ThrowsAsync<SearchStoreException>(
            () => Manager(es).GetStatusAsync(Ct));

        Assert.Contains("index_not_found_exception", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("doesn't exist", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ensure_creates_nothing_when_a_lookup_fails_that_way()
    {
        FakeElasticsearch es = new(request => IsAliasLookup(request, "t-channels")
            ? (HttpStatusCode.NotFound, IndexDeletedMeanwhile)
            : Existing(request));

        await Assert.ThrowsAsync<SearchStoreException>(() => Manager(es).EnsureAsync(Ct));

        Assert.DoesNotContain(es.Requests, r => r.Method != HttpMethod.Get);
    }

    [Fact]
    public async Task A_missing_alias_is_still_missing()
    {
        FakeElasticsearch es = new(request => IsAliasLookup(request, "t-channels")
            ? (HttpStatusCode.NotFound, AliasMissing)
            : Existing(request));

        SearchStoreException ex = await Assert.ThrowsAsync<SearchStoreException>(
            () => Manager(es).GetStatusAsync(Ct));

        Assert.Equal("Alias t-channels doesn't exist (indices not created yet).", ex.Message);
    }

    [Fact]
    public async Task Ensure_creates_the_indices_whose_aliases_are_missing()
    {
        FakeElasticsearch es = new(request => request.Method == HttpMethod.Get
            && request.RequestUri!.AbsolutePath.StartsWith("/_alias/t-channels", StringComparison.Ordinal)
                ? (HttpStatusCode.NotFound, AliasMissing)
                : Existing(request));

        await Manager(es).EnsureAsync(Ct);

        Assert.Contains(es.Requests, r => r.Method == HttpMethod.Put && r.RequestUri!.AbsolutePath == "/t-channels-v1");
        Assert.DoesNotContain(
            es.Requests,
            r => r.Method == HttpMethod.Put && r.RequestUri!.AbsolutePath == "/t-videos-v1");
    }

    [Fact]
    public async Task Alias_lookups_skip_indices_that_disappear_meanwhile()
    {
        FakeElasticsearch es = new(Existing);

        await Manager(es).GetStatusAsync(Ct);

        HttpRequestMessage[] lookups =
            [.. es.Requests.Where(r => r.RequestUri!.AbsolutePath.StartsWith("/_alias/", StringComparison.Ordinal))];
        Assert.Equal(4, lookups.Length);
        Assert.All(
            lookups,
            r => Assert.Contains("ignore_unavailable=true", r.RequestUri!.Query, StringComparison.Ordinal));
    }

    private static IndexManager Manager(FakeElasticsearch es) =>
        new(
            EsClient.Create(
                new ElasticsearchOptions("http://es.test:9200", "elastic", "password"),
                new HttpRequestInvoker((_, _) => es)),
            new IndexNames("t"),
            NullLogger<IndexManager>.Instance);

    private static bool IsAliasLookup(HttpRequestMessage request, string alias) =>
        request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath == $"/_alias/{alias}";

    /// <summary>A store whose indices and aliases all exist (generation 1), with an up-to-date mapping.</summary>
    private static (HttpStatusCode, string) Existing(HttpRequestMessage request)
    {
        string path = request.RequestUri!.AbsolutePath;
        if (path.StartsWith("/_alias/", StringComparison.Ordinal))
        {
            string alias = path["/_alias/".Length..];
            string index = alias.Replace("-write", "", StringComparison.Ordinal) + "-v1";
            return (HttpStatusCode.OK, "{\"" + index + "\":{\"aliases\":{\"" + alias + "\":{}}}}");
        }

        if (path.EndsWith("/_mapping", StringComparison.Ordinal))
        {
            string index = path[1..^"/_mapping".Length];
            return (HttpStatusCode.OK, "{\"" + index + "\":{\"mappings\":{}}}");
        }

        return path.EndsWith("/_count", StringComparison.Ordinal)
            ? (HttpStatusCode.OK, """{"count":0,"_shards":{"total":1,"successful":1,"skipped":0,"failed":0}}""")
            : (HttpStatusCode.OK, """{"acknowledged":true,"shards_acknowledged":true}""");
    }

    /// <summary>Answers every request from <paramref name="answer"/>, as Elasticsearch would, and keeps them.</summary>
    private sealed class FakeElasticsearch(Func<HttpRequestMessage, (HttpStatusCode Status, string Body)> answer)
        : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            lock (Requests)
            {
                Requests.Add(request);
            }

            (HttpStatusCode status, string body) = answer(request);
            HttpResponseMessage response = new(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
                RequestMessage = request,
            };
            response.Headers.Add("X-Elastic-Product", "Elasticsearch");
            return Task.FromResult(response);
        }
    }
}
