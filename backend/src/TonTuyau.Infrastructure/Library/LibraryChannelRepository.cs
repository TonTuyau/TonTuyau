using TonTuyau.Application.Library;
using TonTuyau.Application.Paging;
using TonTuyau.Application.Store;
using TonTuyau.Infrastructure.Elasticsearch;

namespace TonTuyau.Infrastructure.Library;

/// <summary>The library's channels (2.0). Same rules as <see cref="LibraryVideoRepository"/>.</summary>
internal sealed class LibraryChannelRepository(ChannelStore store, ChannelIndex index) : IChannelRepository
{
    public async Task<UpsertResult> AddMissingAsync(
        IReadOnlyCollection<Channel> channels,
        CancellationToken cancellationToken)
    {
        UpsertResult added = await store.AddMissingAsync(channels, cancellationToken).ConfigureAwait(false);
        await ReindexAsync(added.ChangedIds, cancellationToken).ConfigureAwait(false);
        return added;
    }

    public async Task<UpsertResult> ReindexAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Count == 0)
        {
            return UpsertResult.None;
        }

        IReadOnlyList<StoredChannel> stored = await store.GetStoredAsync(ids, cancellationToken).ConfigureAwait(false);
        HashSet<string> found = stored
            .Select(s => s.Channel.Id)
            .ToHashSet(StringComparer.Ordinal);
        string[] gone = ids
            .Where(id => !found.Contains(id))
            .ToArray();
        if (gone.Length > 0)
        {
            await index.DeleteAsync(gone, cancellationToken).ConfigureAwait(false);
        }

        return await index.IndexAsync(stored, cancellationToken).ConfigureAwait(false);
    }

    public Task<Channel?> GetAsync(string id, CancellationToken cancellationToken) =>
        store.GetAsync(id, cancellationToken);

    public Task<StoredChannel?> GetStoredAsync(string id, CancellationToken cancellationToken) =>
        index.GetStoredAsync(id, cancellationToken);

    public Task<long> CountAsync(CancellationToken cancellationToken) => store.CountAsync(cancellationToken);

    public async Task SetArtAsync(
        ArtSubject subject,
        IReadOnlyDictionary<string, ArtVersions> art,
        CancellationToken cancellationToken)
    {
        await store.SetArtAsync(subject, art, cancellationToken).ConfigureAwait(false);
        await index.SetArtAsync(subject, art, cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<string>> GetIdsAfterAsync(
        string? after,
        int count,
        CancellationToken cancellationToken) =>
        store.GetIdsAfterAsync(after, count, cancellationToken);

    public Task<StorePage<ChannelListItem>> FindAsync(
        ChannelQuery query,
        PageRequest page,
        CancellationToken cancellationToken) =>
        index.FindAsync(query, page, cancellationToken);

    public Task<IReadOnlyDictionary<string, ChannelListItem>> GetListItemsAsync(
        IReadOnlyCollection<string> ids,
        CancellationToken cancellationToken) =>
        index.GetListItemsAsync(ids, cancellationToken);

    public Task<IReadOnlyList<TextMatch>> SuggestNamesAsync(
        string prefix,
        int size,
        CancellationToken cancellationToken) =>
        index.SuggestNamesAsync(prefix, size, cancellationToken);
}
