using TonTuyau.Application.Library;

namespace TonTuyau.Application.Store;

/// <summary>
/// The library's channels in PostgreSQL (2.0, <c>channels</c>). Same rules as <see cref="IVideoStore"/>: what
/// TubeArchivist brings only adds a channel TonTuyau doesn't have.
/// </summary>
public interface IChannelStore
{
    /// <summary>
    /// Adds the channels TonTuyau doesn't have, "not checked" on YouTube; the others are left as they are
    /// (<see cref="UpsertResult.Unchanged"/>: already there).
    /// </summary>
    /// <param name="origin">Where they come from: TubeArchivist's import, or TonTuyau's own downloads.</param>
    Task<UpsertResult> AddMissingAsync(
        IReadOnlyCollection<Channel> channels,
        LibraryOrigin origin,
        CancellationToken cancellationToken);

    Task<Channel?> GetAsync(string id, CancellationToken cancellationToken);

    /// <returns>The channel with its pictures' recorded versions, or null when there's none with this id.</returns>
    Task<StoredChannel?> GetStoredAsync(string id, CancellationToken cancellationToken);

    Task<long> CountAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Records what the art store holds for these channels' pictures of one kind (thumb or banner). Unknown ids are
    /// ignored.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Not a channel thumb or banner.</exception>
    Task SetArtAsync(
        ArtSubject subject,
        IReadOnlyDictionary<string, ArtVersions> art,
        CancellationToken cancellationToken);

    /// <summary>The library's channel ids in order, a page at a time (an index rebuild walks them all).</summary>
    /// <param name="after">The previous page's last id; null for the first page.</param>
    /// <returns>At most <paramref name="count"/> ids; fewer at the end, none past it.</returns>
    Task<IReadOnlyList<string>> GetIdsAfterAsync(string? after, int count, CancellationToken cancellationToken);
}
