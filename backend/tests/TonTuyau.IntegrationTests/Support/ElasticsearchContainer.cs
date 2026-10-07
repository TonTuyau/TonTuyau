using System.Globalization;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace TonTuyau.IntegrationTests.Support;

/// <summary>
/// One real Elasticsearch for the whole test run, started on first use by an @elasticsearch scenario.
/// Same security setup as docker-compose (auth on, plain HTTP). Small heap: the dev laptop has 16 GB, often busy.
/// </summary>
public static class ElasticsearchContainer
{
    public const string Image = "docker.elastic.co/elasticsearch/elasticsearch:9.5.4";
    public const string Password = "integration-es-password";
    public const long MinFreeMemoryMb = 2048;

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static IContainer? _container;

    /// <summary>
    /// Deletes one scenario's indices (every index whose name starts with its prefix), so the node's cluster state stays
    /// small over a run of hundreds of scenarios (it grew slow enough to time cluster events out).
    /// </summary>
    public static async Task DeleteIndicesAsync(string prefix)
    {
        if (_container is null)
        {
            return;
        }

        string url = await GetUrlAsync().ConfigureAwait(false);
        using HttpClient http = new() { BaseAddress = new Uri(url) };
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"elastic:{Password}")));
        string names = await http
            .GetStringAsync(new Uri($"/_cat/indices/{prefix}-*?h=index&expand_wildcards=all", UriKind.Relative))
            .ConfigureAwait(false);
        string[] indices = names.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (indices.Length > 0)
        {
            using HttpResponseMessage deleted = await http
                .DeleteAsync(new Uri("/" + string.Join(',', indices), UriKind.Relative))
                .ConfigureAwait(false);
            deleted.EnsureSuccessStatusCode();
        }
    }

    public static async Task<string> GetUrlAsync()
    {
        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_container is null)
            {
                EnsureEnoughMemory();
                IContainer container = new ContainerBuilder(Image)
                    .WithEnvironment("discovery.type", "single-node")
                    .WithEnvironment("xpack.security.enabled", "true")
                    .WithEnvironment("xpack.security.http.ssl.enabled", "false")
                    .WithEnvironment("xpack.ml.enabled", "false")
                    .WithEnvironment("ELASTIC_PASSWORD", Password)
                    .WithEnvironment("ES_JAVA_OPTS", "-Xms512m -Xmx512m")
                    .WithEnvironment("cluster.routing.allocation.disk.threshold_enabled", "false")
                    // Every scenario makes its own indices (4 shards) and the run keeps them: past ~250 scenarios the
                    // default limit of 1,000 shards per node refuses new indices.
                    .WithEnvironment("cluster.max_shards_per_node", "4000")
                    .WithPortBinding(9200, true)
                    .WithCreateParameterModifier(p => p.HostConfig!.Memory = 1536L * 1024 * 1024)
                    .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r
                        .ForPort(9200)
                        .ForPath("/_cluster/health")
                        .WithBasicAuthentication("elastic", Password)))
                    .Build();
                await container.StartAsync().ConfigureAwait(false);
                _container = container;
            }

            string port = _container.GetMappedPublicPort(9200).ToString(CultureInfo.InvariantCulture);
            return $"http://{_container.Hostname}:{port}";
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task StopAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync().ConfigureAwait(false);
            _container = null;
        }
    }

    private static void EnsureEnoughMemory()
    {
        const string meminfo = "/proc/meminfo";
        if (!File.Exists(meminfo))
        {
            return;
        }

        string? line = File.ReadLines(meminfo)
            .FirstOrDefault(l => l.StartsWith("MemAvailable:", StringComparison.Ordinal));
        long kb = line is null
            ? 0
            : long.Parse(line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1], CultureInfo.InvariantCulture);
        if (kb / 1024 < MinFreeMemoryMb)
        {
            throw new InvalidOperationException(
                $"Only {kb / 1024} MB of RAM available; Elasticsearch tests need {MinFreeMemoryMb} MB. Close something (e.g. Godot) and re-run.");
        }
    }
}
