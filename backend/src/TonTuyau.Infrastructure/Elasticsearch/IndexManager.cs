using System.Globalization;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Microsoft.Extensions.Logging;
using TonTuyau.Application.Store;

namespace TonTuyau.Infrastructure.Elasticsearch;

internal sealed partial class IndexManager(
    ElasticsearchClient client,
    IndexNames names,
    ILogger<IndexManager> logger) : IIndexManager
{
    private static readonly StoreIndex[] All = Enum.GetValues<StoreIndex>();

    public async Task EnsureAsync(CancellationToken cancellationToken)
    {
        foreach (StoreIndex index in All)
        {
            string? read = await AliasTargetAsync(names.ReadAlias(index), cancellationToken).ConfigureAwait(false);
            string? write = await AliasTargetAsync(names.WriteAlias(index), cancellationToken).ConfigureAwait(false);
            if (read is not null && write is not null)
            {
                continue;
            }

            string target = read ?? write ?? names.Generation(index, 1);
            if (read is null && write is null)
            {
                await client
                    .CreateIndexFromDefinitionAsync(
                        target,
                        IndexDefinitions.For(index).ToJsonString(),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            List<IndexUpdateAliasesAction> actions = [];
            if (read is null)
            {
                actions.Add(Add(target, names.ReadAlias(index)));
            }

            if (write is null)
            {
                actions.Add(Add(target, names.WriteAlias(index)));
            }

            await UpdateAliasesAsync(actions, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<IndexStatus>> GetStatusAsync(CancellationToken cancellationToken)
    {
        List<IndexStatus> statuses = [];
        foreach (StoreIndex index in All)
        {
            string read = await AliasTargetAsync(names.ReadAlias(index), cancellationToken).ConfigureAwait(false)
                ?? throw new SearchStoreException(
                    $"Alias {names.ReadAlias(index)} doesn't exist (indices not created yet).");
            string write = await AliasTargetAsync(names.WriteAlias(index), cancellationToken).ConfigureAwait(false)
                ?? read;

            GetMappingResponse mapping = (await client.Indices
                .GetMappingAsync(new GetMappingRequest(read), cancellationToken)
                .ConfigureAwait(false))
                .Ensure($"Mapping of {read}");
            string? hash = mapping.Mappings.TryGetValue(read, out IndexMappingRecord? record)
                && record.Mappings.Meta is not null
                && record.Mappings.Meta.TryGetValue("definition_hash", out object? value)
                    ? value?.ToString()
                    : null;
            CountResponse count = (await client
                .CountAsync(new CountRequest(read), cancellationToken)
                .ConfigureAwait(false))
                .Ensure($"Count {read}");

            statuses.Add(new IndexStatus(index, read, write, hash == IndexDefinitions.HashOf(index), count.Count));
        }

        return statuses;
    }

    public async Task<IndexRebuild> BeginRebuildAsync(CancellationToken cancellationToken)
    {
        Dictionary<StoreIndex, string> oldIndices = [];
        Dictionary<StoreIndex, string> newIndices = [];
        foreach (StoreIndex index in All)
        {
            string read = await AliasTargetAsync(names.ReadAlias(index), cancellationToken).ConfigureAwait(false)
                ?? throw new SearchStoreException($"Alias {names.ReadAlias(index)} doesn't exist: nothing to rebuild.");
            string write = await AliasTargetAsync(names.WriteAlias(index), cancellationToken).ConfigureAwait(false)
                ?? read;
            if (write != read)
            {
                throw new SearchStoreException(
                    $"A rebuild is already in progress for {names.ReadAlias(index)} (writes go to {write}).");
            }

            string next = names.Generation(index, (names.VersionOf(index, read) ?? 0) + 1);
            await DeleteLeftoverAsync(next, cancellationToken).ConfigureAwait(false);
            await client
                .CreateIndexFromDefinitionAsync(next, IndexDefinitions.For(index).ToJsonString(), cancellationToken)
                .ConfigureAwait(false);
            oldIndices[index] = read;
            newIndices[index] = next;
        }

        await MoveAliasAsync(names.WriteAlias, oldIndices, newIndices, cancellationToken).ConfigureAwait(false);
        return new IndexRebuild(oldIndices, newIndices);
    }

    public async Task CompleteRebuildAsync(IndexRebuild rebuild, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rebuild);
        await MoveAliasAsync(names.ReadAlias, rebuild.OldIndices, rebuild.NewIndices, cancellationToken)
            .ConfigureAwait(false);
        try
        {
            await DeleteIndicesAsync(rebuild.OldIndices.Values, cancellationToken).ConfigureAwait(false);
        }
        catch (SearchStoreException ex)
        {
            // Users read the new generation now: the rebuild succeeded. A failure here must not reach the caller,
            // which would abort the rebuild and delete the new (live) indices. The old ones are left, without aliases.
            LogOldGenerationLeft(ex, string.Join(", ", rebuild.OldIndices.Values));
        }
    }

    public async Task AbortRebuildAsync(IndexRebuild rebuild, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rebuild);
        await MoveAliasAsync(names.WriteAlias, rebuild.NewIndices, rebuild.OldIndices, cancellationToken)
            .ConfigureAwait(false);
        await DeleteIndicesAsync(rebuild.NewIndices.Values, cancellationToken).ConfigureAwait(false);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        string targets = string.Join(',', All.SelectMany(i => new[] { names.ReadAlias(i), names.WriteAlias(i) }));
        (await client.Indices
            .RefreshAsync(new RefreshRequest(Indices.Parse(targets, CultureInfo.InvariantCulture)), cancellationToken)
            .ConfigureAwait(false))
            .Ensure($"Refresh {targets}");
    }

    private static IndexUpdateAliasesAction Add(string index, string alias) =>
        new() { Add = new AddAction { Index = index, Alias = alias } };

    private static IndexUpdateAliasesAction Remove(string index, string alias) =>
        new() { Remove = new RemoveAction { Index = index, Alias = alias } };

    /// <returns>The concrete index the alias points to, or null when Elasticsearch says the alias doesn't exist.</returns>
    /// <remarks>
    /// <c>GET _alias/{name}</c> looks through every index of the cluster. With security on (as in TonTuyau's stack),
    /// Elasticsearch first lists the indices the user may read, then resolves that list: an index deleted in between
    /// (anyone's: another prefix's, another application's, a rebuild's old generation) failed the whole call with
    /// <c>404 index_not_found_exception</c>, which was taken for "alias missing" (QA14-10). <c>ignore_unavailable</c>
    /// skips such an index, and only Elasticsearch's own "alias [x] missing" answer means missing: any other 404 is
    /// an error, never a reason to create indices or to say they don't exist.
    /// </remarks>
    private async Task<string?> AliasTargetAsync(string alias, CancellationToken cancellationToken)
    {
        GetAliasRequest request = new(Names.Parse(alias, CultureInfo.InvariantCulture)) { IgnoreUnavailable = true };
        GetAliasResponse response = await client.Indices
            .GetAliasAsync(request, cancellationToken)
            .ConfigureAwait(false);
        if (response.Status() == 404 && IsAliasMissing(response))
        {
            return null;
        }

        response.Ensure($"Alias {alias}");
        if (response.Aliases is not { Count: > 0 } targets)
        {
            return null;
        }

        if (targets.Count > 1)
        {
            string all = string.Join(", ", targets.Keys);
            throw new SearchStoreException($"Alias {alias} points to {targets.Count} indices ({all}); expected one.");
        }

        return targets.Keys.Single().ToString();
    }

    /// <summary>
    /// The 404 of a missing alias: <c>{"error": "alias [x] missing", "status": 404}</c>, an error without a type.
    /// A missing index answers <c>index_not_found_exception</c>, a typed error.
    /// </summary>
    private static bool IsAliasMissing(GetAliasResponse response) =>
        response.ElasticsearchServerError?.Error is not null
        && string.IsNullOrEmpty(response.ElasticsearchServerError.Error.Type)
        && response.ElasticsearchServerError.Error.Reason?.EndsWith(" missing", StringComparison.Ordinal) == true;

    /// <summary>
    /// Moves one alias of every index from one generation to another in a single <c>_aliases</c> request, so all
    /// indices switch at the same instant.
    /// </summary>
    private Task MoveAliasAsync(
        Func<StoreIndex, string> alias,
        IReadOnlyDictionary<StoreIndex, string> from,
        IReadOnlyDictionary<StoreIndex, string> to,
        CancellationToken cancellationToken)
    {
        List<IndexUpdateAliasesAction> actions = [];
        foreach (StoreIndex index in All)
        {
            actions.Add(Remove(from[index], alias(index)));
            actions.Add(Add(to[index], alias(index)));
        }

        return UpdateAliasesAsync(actions, cancellationToken);
    }

    /// <summary>
    /// A generation left behind by an abort whose delete failed: nothing points to it, so nothing depends on it.
    /// One that an alias still points to is never deleted.
    /// </summary>
    private async Task DeleteLeftoverAsync(string index, CancellationToken cancellationToken)
    {
        GetIndexResponse response = (await client.Indices
            .GetAsync(new GetIndexRequest(index), cancellationToken)
            .ConfigureAwait(false))
            .Ensure($"Get index {index}", 404);
        if (response.Status() == 404)
        {
            return;
        }

        string[] used = response.Indices.TryGetValue(index, out IndexState? state)
            && state.Aliases is not null
            ? state.Aliases.Keys
                .Select(a => a.ToString())
                .ToArray()
            : [];
        if (used.Length > 0)
        {
            throw new SearchStoreException(
                $"{index} already exists and is used by {string.Join(", ", used)}: refusing to replace it.");
        }

        await DeleteIndicesAsync([index], cancellationToken).ConfigureAwait(false);
    }

    private async Task UpdateAliasesAsync(List<IndexUpdateAliasesAction> actions, CancellationToken cancellationToken)
    {
        if (actions.Count > 0)
        {
            (await client.Indices
                .UpdateAliasesAsync(new UpdateAliasesRequest { Actions = actions }, cancellationToken)
                .ConfigureAwait(false))
                .Ensure("Update aliases");
        }
    }

    private async Task DeleteIndicesAsync(IEnumerable<string> indices, CancellationToken cancellationToken)
    {
        foreach (string index in indices)
        {
            (await client.Indices
                .DeleteAsync(new DeleteIndexRequest(index), cancellationToken)
                .ConfigureAwait(false))
                .Ensure($"Delete index {index}", 404);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Index rebuild done, but the previous generation ({Indices}) couldn't be deleted: delete it by hand.")]
    private partial void LogOldGenerationLeft(Exception exception, string indices);
}
