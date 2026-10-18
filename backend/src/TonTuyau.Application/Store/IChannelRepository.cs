using TonTuyau.Application.Paging;

namespace TonTuyau.Application.Store;

/// <summary>
/// The library's channels with what the index (Elasticsearch) answers. Same rules as <see cref="IVideoRepository"/>.
/// </summary>
public interface IChannelRepository : IChannelStore
{
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
