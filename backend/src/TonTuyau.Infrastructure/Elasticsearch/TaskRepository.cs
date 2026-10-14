using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.QueryDsl;
using TonTuyau.Application.Paging;
using TonTuyau.Application.Sync;
using TonTuyau.Application.Tasks;

namespace TonTuyau.Infrastructure.Elasticsearch;

/// <summary>
/// The Tasks page's reads of the run history, latest first: sorted by start, then id (two runs can start within the
/// same second), and paged after the previous page's last run (<c>search_after</c>).
/// </summary>
internal sealed class TaskRepository(ElasticsearchClient client, IndexNames names) : ITaskRepository
{
    private static readonly SortOptions[] LatestFirst =
    [
        new() { Field = new FieldSort { Field = Infer.Field<SyncRun>(r => r.StartedAt), Order = SortOrder.Desc } },
        new() { Field = new FieldSort { Field = Infer.Field<SyncRun>(r => r.Id), Order = SortOrder.Desc } },
    ];

    /// <summary>How many runs that applied a list are looked through for one full run's (there are few).</summary>
    private const int AppliesLookedAt = 50;

    public async Task<SyncRun?> GetLatestRunAsync(SyncMode mode, CancellationToken cancellationToken)
    {
        SearchResponse<SyncRun> response = await SearchAsync(OfMode(mode), 1, null, cancellationToken)
            .ConfigureAwait(false);
        return response.Hits
            .Select(h => h.Source)
            .FirstOrDefault();
    }

    public async Task<ResultPage<SyncRun>> GetRunsAsync(
        SyncMode? mode,
        SyncRun? before,
        int count,
        CancellationToken cancellationToken)
    {
        Query query = mode is null ? new MatchAllQuery() : OfMode(mode.Value);
        // One more than asked: whether a next page exists.
        SearchResponse<SyncRun> response = await SearchAsync(query, count + 1, before, cancellationToken)
            .ConfigureAwait(false);
        SyncRun[] hits = response.Hits
            .Select(h => h.Source)
            .OfType<SyncRun>()
            .ToArray();
        SyncRun[] page = hits[..Math.Min(count, hits.Length)];
        string? next = hits.Length > count ? page[^1].Id : null;
        return new ResultPage<SyncRun>(page, next, response.Total);
    }

    public async Task<bool> WasAppliedAsync(string fullRunId, CancellationToken cancellationToken)
    {
        // applies_to isn't indexed (the mapping indexes what's searched for, and isn't changed for this): the latest
        // runs that applied a list are read, and their source looked at.
        BoolQuery succeededApplies = new()
        {
            Filter =
            [
                OfMode(SyncMode.ApplyDeletions),
                new TermQuery { Field = Infer.Field<SyncRun>(r => r.Outcome), Value = "succeeded" },
            ],
        };
        SearchResponse<SyncRun> response = await SearchAsync(succeededApplies, AppliesLookedAt, null, cancellationToken)
            .ConfigureAwait(false);
        return response.Hits.Any(h => h.Source?.AppliesTo == fullRunId);
    }

    private static TermQuery OfMode(SyncMode mode) =>
        new()
        {
            Field = Infer.Field<SyncRun>(r => r.Mode),
            Value = mode switch
            {
                SyncMode.Incremental => "incremental",
                SyncMode.Full => "full",
                _ => "apply_deletions",
            },
        };

    /// <summary>Latest first, after <paramref name="before"/> when given.</summary>
    private async Task<SearchResponse<SyncRun>> SearchAsync(
        Query query,
        int size,
        SyncRun? before,
        CancellationToken cancellationToken)
    {
        SearchRequest<SyncRun> request = new(names.SyncRuns)
        {
            Size = size,
            Query = query,
            Sort = LatestFirst,
            // A date sorts as its Unix milliseconds.
            SearchAfter = before is null
                ? null
                : [FieldValue.Long(before.StartedAt.ToUnixTimeMilliseconds()), FieldValue.String(before.Id)],
            TrackTotalHits = new TrackHits(true),
        };
        return (await client
            .SearchAsync<SyncRun>(request, cancellationToken)
            .ConfigureAwait(false))
            .Ensure($"Search {names.SyncRuns}");
    }
}
