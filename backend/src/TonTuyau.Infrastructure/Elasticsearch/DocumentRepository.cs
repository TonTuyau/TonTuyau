using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Bulk;
using Elastic.Clients.Elasticsearch.Core.MGet;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.QueryDsl;
using TonTuyau.Application.Paging;
using TonTuyau.Application.Store;
using TonTuyau.Infrastructure.Elasticsearch.Documents;

namespace TonTuyau.Infrastructure.Elasticsearch;

/// <summary>
/// Upsert-by-id with content-hash skipping, get, count, delete, and the queries the sync needs. Shared by the video
/// and channel repositories.
/// </summary>
/// <remarks>
/// Bookkeeping fields (<c>seen_in_run</c>, <c>media_missing</c> for videos, the art records) are not content: a
/// rewrite of a document's content keeps the values it had.
/// </remarks>
internal abstract class DocumentRepository<TDomain, TDocument>(
    ElasticsearchClient client,
    IndexNames names,
    StoreIndex index)
    where TDocument : class, IStoreDocument
{
    public const int BatchSize = 500;
    public const int SearchPageSize = 1000;

    protected ElasticsearchClient Client => client;

    protected string ReadAlias => names.ReadAlias(index);

    protected string WriteAlias => names.WriteAlias(index);

    public async Task<UpsertResult> UpsertAsync(
        IReadOnlyCollection<TDomain> items,
        string? runId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);

        // Same id twice in one call: the last one wins, like successive upserts would.
        TDocument[] documents = items
            .Select(ToDocument)
            .GroupBy(d => d.Id, StringComparer.Ordinal)
            .Select(g => g.Last())
            .ToArray();
        UpsertResult result = UpsertResult.None;
        foreach (TDocument[] batch in documents.Chunk(BatchSize))
        {
            result = result.Add(await UpsertBatchAsync(batch, runId, cancellationToken).ConfigureAwait(false));
        }

        return result;
    }

    public async Task<TDomain?> GetAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        GetResponse<TDocument> response = (await client
            .GetAsync<TDocument>(new GetRequest(ReadAlias, id), cancellationToken)
            .ConfigureAwait(false))
            .Ensure($"Get {ReadAlias}/{id}", 404);
        return response.Found && response.Source is not null ? ToDomain(response.Source) : default;
    }

    public async Task<long> CountAsync(CancellationToken cancellationToken)
    {
        CountResponse response = (await client
            .CountAsync(new CountRequest(ReadAlias), cancellationToken)
            .ConfigureAwait(false))
            .Ensure($"Count {ReadAlias}");
        return response.Count;
    }

    public async Task<int> DeleteAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        int deleted = 0;
        IEnumerable<string[]> batches = ids
            .Distinct(StringComparer.Ordinal)
            .Chunk(BatchSize);
        foreach (string[] batch in batches)
        {
            BulkRequest bulk = new()
            {
                Operations = [.. batch.Select(id => new BulkDeleteOperation(id) { Index = WriteAlias })],
            };
            BulkResponse response = await BulkAsync(bulk, cancellationToken).ConfigureAwait(false);
            deleted += response.Items.Count(i => i.Result == "deleted");
        }

        return deleted;
    }

    public Task MarkSeenAsync(IReadOnlyCollection<string> ids, string runId, CancellationToken cancellationToken) =>
        PartialUpdateAsync(ids, new SeenInRunPartial(runId), cancellationToken);

    protected abstract TDocument ToDocument(TDomain item);

    protected abstract TDomain ToDomain(TDocument document);

    /// <summary>
    /// The document with the bookkeeping it must carry: the run's mark (or the one it had), and what it had before for
    /// the rest (<paramref name="previous"/> is null for a new document).
    /// </summary>
    protected abstract TDocument WithBookkeeping(TDocument document, string? seenInRun, StoreBookkeeping? previous);

    /// <summary>Sets the same partial document on every id that exists (unknown ids are ignored).</summary>
    protected async Task PartialUpdateAsync<TPartial>(
        IReadOnlyCollection<string> ids,
        TPartial partial,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        IEnumerable<string[]> batches = ids
            .Distinct(StringComparer.Ordinal)
            .Chunk(BatchSize);
        foreach (string[] batch in batches)
        {
            BulkRequest bulk = new()
            {
                Operations = [.. batch.Select(
                    id => new BulkUpdateOperationWithPartial<TPartial>(id, WriteAlias, partial))],
            };
            await BulkAsync(bulk, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>A partial document per id (e.g. each one's art record). Unknown ids are ignored.</summary>
    protected async Task PartialUpdateEachAsync<TPartial>(
        IEnumerable<KeyValuePair<string, TPartial>> partials,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(partials);
        foreach (KeyValuePair<string, TPartial>[] batch in partials.Chunk(BatchSize))
        {
            BulkRequest bulk = new()
            {
                Operations = [.. batch.Select(
                    p => new BulkUpdateOperationWithPartial<TPartial>(p.Key, WriteAlias, p.Value))],
            };
            await BulkAsync(bulk, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Every document matching <paramref name="query"/> on the write alias (during a rebuild: the generation being
    /// built), paged with <c>search_after</c> on the id, so there's no 10k window. Returns only <paramref name="fields"/>.
    /// </summary>
    protected async Task<IReadOnlyList<TSource>> SearchAllAsync<TSource>(
        Query query,
        Fields fields,
        CancellationToken cancellationToken)
    {
        List<TSource> results = [];
        ICollection<FieldValue>? after = null;
        while (true)
        {
            SearchRequest<TDocument> request = new(WriteAlias)
            {
                Size = SearchPageSize,
                Query = query,
                Source = new SourceConfig(new SourceFilter { Includes = fields }),
                Sort = [new SortOptions {
                    Field = new FieldSort { Field = Infer.Field<TDocument>(d => d.Id), Order = SortOrder.Asc } }],
                SearchAfter = after,
            };
            SearchResponse<TSource> response = (await client
                .SearchAsync<TSource>(request, cancellationToken)
                .ConfigureAwait(false))
                .Ensure($"Search {WriteAlias}");
            results.AddRange(response.Hits
                .Select(h => h.Source)
                .OfType<TSource>());

            if (response.Hits.Count < SearchPageSize)
            {
                return results;
            }

            after = response.Hits.Last().Sort?.ToArray();
        }
    }

    /// <summary>
    /// One page of a sorted list on the read alias (what users see), with the exact total. Asks for one more hit than
    /// the page holds: if it comes back, there is a next page, starting after the page's last hit.
    /// </summary>
    protected async Task<StorePage<TItem>> FindPageAsync<TSource, TItem>(
        Query query,
        IReadOnlyList<SortKey> sort,
        PageRequest page,
        Fields fields,
        Func<TSource, TItem> map,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        SearchRequest<TDocument> request = new(ReadAlias)
        {
            Query = query,
            Size = page.Size + 1,
            Sort = [.. sort.Select(k => k.Options)],
            SearchAfter = page.After is null ? null : SortPositions.ToSearchAfter(page.After, sort),
            TrackTotalHits = new TrackHits(true),
            Source = new SourceConfig(new SourceFilter { Includes = fields }),
        };
        SearchResponse<TSource> response = (await client
            .SearchAsync<TSource>(request, cancellationToken)
            .ConfigureAwait(false))
            .Ensure($"Search {ReadAlias}");
        Hit<TSource>[] shown = response.Hits
            .Take(page.Size)
            .ToArray();
        SortPosition? next = response.Hits.Count > page.Size ? SortPositions.From(shown[^1].Sort) : null;
        TItem[] items = shown
            .Select(h => map(h.Source!))
            .ToArray();
        return new StorePage<TItem>(items, next, response.Total);
    }

    /// <summary>The first hits of a query on the read alias, sorted (suggestions: no paging, no total).</summary>
    protected async Task<IReadOnlyList<TSource>> FindTopAsync<TSource>(
        Query query,
        IReadOnlyList<SortKey> sort,
        int size,
        Fields fields,
        CancellationToken cancellationToken)
    {
        SearchRequest<TDocument> request = new(ReadAlias)
        {
            Query = query,
            Size = size,
            Sort = [.. sort.Select(k => k.Options)],
            TrackTotalHits = new TrackHits(false),
            Source = new SourceConfig(new SourceFilter { Includes = fields }),
        };
        SearchResponse<TSource> response = (await client
            .SearchAsync<TSource>(request, cancellationToken)
            .ConfigureAwait(false))
            .Ensure($"Search {ReadAlias}");
        return response.Hits
            .Select(h => h.Source)
            .OfType<TSource>()
            .ToArray();
    }

    private async Task<UpsertResult> UpsertBatchAsync(
        TDocument[] batch,
        string? runId,
        CancellationToken cancellationToken)
    {
        Dictionary<string, StoreBookkeeping> existing = await GetBookkeepingAsync(batch, cancellationToken)
            .ConfigureAwait(false);

        int created = 0, updated = 0, unchanged = 0;
        List<string> changed = [];
        List<string> createdIds = [];
        List<IBulkOperation> operations = [];
        foreach (TDocument document in batch)
        {
            bool found = existing.TryGetValue(document.Id, out StoreBookkeeping? previous);
            if (found && previous!.ContentHash == document.ContentHash)
            {
                unchanged++;
                if (runId is not null && previous.SeenInRun != runId)
                {
                    operations.Add(new BulkUpdateOperationWithPartial<SeenInRunPartial>(
                        document.Id,
                        WriteAlias,
                        new SeenInRunPartial(runId)));
                }

                continue;
            }

            if (found)
            {
                updated++;
            }
            else
            {
                created++;
                createdIds.Add(document.Id);
            }

            // A content rewrite keeps the bookkeeping it had; a run's id replaces the old "seen" mark.
            changed.Add(document.Id);
            TDocument toWrite = WithBookkeeping(document, runId ?? previous?.SeenInRun, previous);
            operations.Add(new BulkIndexOperation<TDocument>(toWrite, WriteAlias) { Id = document.Id });
        }

        if (operations.Count > 0)
        {
            await BulkAsync(new BulkRequest { Operations = [.. operations] }, cancellationToken).ConfigureAwait(false);
        }

        return new UpsertResult(created, updated, unchanged) { ChangedIds = changed, CreatedIds = createdIds };
    }

    private async Task<Dictionary<string, StoreBookkeeping>> GetBookkeepingAsync(
        TDocument[] batch,
        CancellationToken cancellationToken)
    {
        MultiGetRequest request = new(WriteAlias)
        {
            Ids = new Ids(batch.Select(d => d.Id)),
            SourceIncludes = Infer.Fields<StoreBookkeeping>(
                b => b.ContentHash!,
                b => b.SeenInRun!,
                b => b.MediaMissing!,
                b => b.ThumbnailArt!,
                b => b.ThumbArt!,
                b => b.BannerArt!),
        };
        MultiGetResponse<StoreBookkeeping> response = (await client
            .MultiGetAsync<StoreBookkeeping>(request, cancellationToken)
            .ConfigureAwait(false))
            .Ensure($"Multi-get {WriteAlias}");

        Dictionary<string, StoreBookkeeping> result = new(StringComparer.Ordinal);
        foreach (MultiGetResponseItem<StoreBookkeeping> item in response.Docs)
        {
            item.Match(
                found =>
                {
                    if (found is { Found: true, Source: { } source })
                    {
                        result[found.Id] = source;
                    }
                },
                error => throw new SearchStoreException(
                    $"Multi-get {WriteAlias}: {error?.Id}: {error?.Error?.Reason}"));
        }

        return result;
    }

    private async Task<BulkResponse> BulkAsync(BulkRequest request, CancellationToken cancellationToken)
    {
        BulkResponse response = await client.BulkAsync(request, cancellationToken).ConfigureAwait(false);
        string[] failures = response.ItemsWithErrors

            // 404 = the id doesn't exist (delete or partial update of an unknown id): not an error here.
            .Where(i => i.Status != 404)
            .Take(3)
            .Select(i => $"{i.Id}: {i.Error?.Type} {i.Error?.Reason}")
            .ToArray();
        if (failures.Length > 0)
        {
            throw new SearchStoreException($"Bulk write to {WriteAlias} failed: " + string.Join("; ", failures));
        }

        if (!response.IsValidResponse && response.Status() is null)
        {
            throw new SearchStoreException(
                $"Bulk write to {WriteAlias}: Elasticsearch can't be reached.",
                response.ApiCallDetails.OriginalException);
        }

        return response;
    }
}
