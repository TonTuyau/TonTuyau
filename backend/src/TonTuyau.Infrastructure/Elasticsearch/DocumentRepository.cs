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
/// The index's copy of the library (2.0: written from PostgreSQL): index by id with content-hash skipping, delete,
/// partial updates, the read alias's searches. Shared by the video and channel indices.
/// </summary>
/// <remarks>
/// A document carries the bookkeeping the lists need (<c>media_missing</c> for videos, the art records): its copy is
/// written whole from PostgreSQL, the bookkeeping updated in place when only it changes.
/// </remarks>
internal abstract class DocumentRepository<TStored, TDocument>(
    ElasticsearchClient client,
    IndexNames names,
    StoreIndex index)
    where TDocument : class, IStoreDocument
{
    public const int BatchSize = 500;

    protected ElasticsearchClient Client => client;

    protected string ReadAlias => names.ReadAlias(index);

    protected string WriteAlias => names.WriteAlias(index);

    /// <summary>
    /// Writes these documents on the write alias (during a rebuild: the generation being built). One whose content
    /// is already there (same hash) isn't rewritten.
    /// </summary>
    public async Task<UpsertResult> IndexAsync(IReadOnlyCollection<TStored> items, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);

        // Same id twice in one call: the last one wins, like successive writes would.
        TDocument[] documents = items
            .Select(ToDocument)
            .GroupBy(d => d.Id, StringComparer.Ordinal)
            .Select(g => g.Last())
            .ToArray();
        UpsertResult result = UpsertResult.None;
        foreach (TDocument[] batch in documents.Chunk(BatchSize))
        {
            result = result.Add(await IndexBatchAsync(batch, cancellationToken).ConfigureAwait(false));
        }

        return result;
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

    /// <summary>The whole document: the content (its hash included) and the bookkeeping, from PostgreSQL.</summary>
    protected abstract TDocument ToDocument(TStored item);

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

    private async Task<UpsertResult> IndexBatchAsync(TDocument[] batch, CancellationToken cancellationToken)
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

            changed.Add(document.Id);
            operations.Add(new BulkIndexOperation<TDocument>(document, WriteAlias) { Id = document.Id });
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
            SourceIncludes = Infer.Fields<StoreBookkeeping>(b => b.ContentHash!),
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
