using TonTuyau.Application.Library;
using TonTuyau.Application.Paging;

namespace TonTuyau.Application.Store;

/// <summary>TonTuyau's own copy of the channels (Elasticsearch). Same rules as <see cref="IVideoRepository"/>.</summary>
public interface IChannelRepository
{
    Task<UpsertResult> UpsertAsync(
        IReadOnlyCollection<Channel> channels,
        string? runId,
        CancellationToken cancellationToken);

    Task<Channel?> GetAsync(string id, CancellationToken cancellationToken);

    Task<long> CountAsync(CancellationToken cancellationToken);

    Task<int> DeleteAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken);

    /// <summary>Marks stored channels as seen by a run without rewriting them. Unknown ids are ignored.</summary>
    Task MarkSeenAsync(IReadOnlyCollection<string> ids, string runId, CancellationToken cancellationToken);

    /// <returns>Channels the run didn't see.</returns>
    Task<IReadOnlyList<ChannelSummary>> FindNotSeenAsync(string runId, CancellationToken cancellationToken);

    /// <summary>
    /// Records what the art store holds for these channels' pictures of one kind (thumb or banner). Not content, kept
    /// by a content rewrite. Unknown ids are ignored.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Not a channel thumb or banner.</exception>
    Task SetArtAsync(
        ArtSubject subject,
        IReadOnlyDictionary<string, ArtVersions> art,
        CancellationToken cancellationToken);

    // Browsing and search (the REST API), on the read alias.

    /// <returns>The channel with its pictures' recorded versions, or null when there's none with this id.</returns>
    Task<StoredChannel?> GetStoredAsync(string id, CancellationToken cancellationToken);

    /// <summary>One page of channels matching the query, sorted, with the exact total.</summary>
    /// <exception cref="Validation.ValidationException">The position doesn't fit the query's sort.</exception>
    Task<StorePage<ChannelListItem>> FindAsync(
        ChannelQuery query,
        PageRequest page,
        CancellationToken cancellationToken);

    /// <returns>The channels found among these ids, by id (unknown ids are left out).</returns>
    Task<IReadOnlyDictionary<string, ChannelListItem>> GetListItemsAsync(
        IReadOnlyCollection<string> ids,
        CancellationToken cancellationToken);

    /// <returns>Channels whose name starts words with what was typed (accents and case ignored), biggest first.</returns>
    Task<IReadOnlyList<TextMatch>> SuggestNamesAsync(string prefix, int size, CancellationToken cancellationToken);
}
