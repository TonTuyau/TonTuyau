using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Cluster;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;
using EsHealth = Elastic.Clients.Elasticsearch.HealthStatus;

namespace TonTuyau.Infrastructure.Elasticsearch;

/// <summary>Set once the indices exist. Until then the backend runs but isn't ready.</summary>
internal sealed class SearchStoreReadiness
{
    private volatile bool _ready;

    public bool IsReady => _ready;

    public void MarkReady() => _ready = true;
}

/// <summary>Ready = Elasticsearch answers with a usable cluster status and TonTuyau's indices exist.</summary>
/// <remarks>
/// A pending or running index rebuild (QA10 M5-10) doesn't make the backend unready: users keep reading the current
/// generation meanwhile. It's said in the check's description.
/// </remarks>
internal sealed partial class SearchStoreHealthCheck(
    ElasticsearchClient client,
    SearchStoreReadiness readiness,
    IndexRebuildState rebuild,
    ILogger<SearchStoreHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            HealthResponse health = (await client.Cluster
                .HealthAsync(new HealthRequest(), cancellationToken)
                .ConfigureAwait(false))
                .Ensure("Cluster health");
            if (health.Status is not (EsHealth.Green or EsHealth.Yellow))
            {
                return HealthCheckResult.Unhealthy($"Elasticsearch cluster status is {health.Status}.");
            }

            string rebuilding = rebuild.Status switch
            {
                IndexRebuildStatus.Pending => " Index rebuild pending.",
                IndexRebuildStatus.Running => " Index rebuild running.",
                _ => "",
            };
            return readiness.IsReady
                ? HealthCheckResult.Healthy($"Elasticsearch {health.Status}, indices ready.{rebuilding}")
                : HealthCheckResult.Unhealthy("TonTuyau's indices aren't created yet.");
        }
        catch (SearchStoreException ex)
        {
            LogUnhealthy(ex);
            return HealthCheckResult.Unhealthy("Elasticsearch can't be used.", ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Readiness check: Elasticsearch can't be used.")]
    private partial void LogUnhealthy(Exception exception);
}

/// <summary>Creates missing indices at startup, retrying until Elasticsearch is there. Never stops the backend.</summary>
internal sealed partial class IndexInitializer(
    IIndexManager indices,
    SearchStoreReadiness readiness,
    IndexRebuildState rebuild,
    ILogger<IndexInitializer> logger) : BackgroundService
{
    public static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await indices.EnsureAsync(stoppingToken).ConfigureAwait(false);
                bool outdated = false;
                foreach (IndexStatus status in await indices.GetStatusAsync(stoppingToken).ConfigureAwait(false))
                {
                    if (!status.DefinitionUpToDate)
                    {
                        outdated = true;
                        LogOutdated(status.ReadIndex);
                    }
                }

                if (rebuild.Status != IndexRebuildStatus.Running)
                {
                    rebuild.Status = outdated ? IndexRebuildStatus.Pending : IndexRebuildStatus.UpToDate;
                }

                readiness.MarkReady();
                LogReady();
                return;
            }
            catch (SearchStoreException ex)
            {
                LogRetry(ex.Message, RetryDelay.TotalSeconds);
            }

            await Task.Delay(RetryDelay, stoppingToken).ConfigureAwait(false);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Search store ready.")]
    private partial void LogReady();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Search store not ready ({Reason}); retrying in {Seconds} s.")]
    private partial void LogRetry(string reason, double seconds);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Index {Index} was created by an older index definition: it needs a rebuild (automatic at startup, "
            + "else at the next import, or now with `tontuyau index rebuild`).")]
    private partial void LogOutdated(string index);
}
