using TonTuyau.Application.Library;

namespace TonTuyau.Application.Store;

/// <summary>
/// The library's channels as the sync keeps them (2.0). Same rules as <see cref="IVideoStore"/>: PostgreSQL holds them
/// (<c>channels</c>); <see cref="IChannelRepository"/> adds what the index answers.
/// </summary>
public interface IChannelStore
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

    /// <returns>The channel with its pictures' recorded versions, or null when there's none with this id.</returns>
    Task<StoredChannel?> GetStoredAsync(string id, CancellationToken cancellationToken);
}
