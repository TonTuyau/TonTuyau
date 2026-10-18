using TonTuyau.Application.Library;

namespace TonTuyau.Application.Store;

/// <summary>
/// The library's videos in PostgreSQL (2.0, <c>videos</c>): the source of truth, one set of columns per video. What
/// TubeArchivist brings only ever adds a video TonTuyau doesn't have: a video it has is left as it is, whatever
/// TubeArchivist says of it, so what TonTuyau read on YouTube is never overwritten.
/// </summary>
public interface IVideoStore
{
    /// <summary>
    /// Adds the videos TonTuyau doesn't have, "not checked" on YouTube (TubeArchivist's own flag isn't kept); the
    /// others are left as they are (<see cref="UpsertResult.Unchanged"/>: already there). The same id twice in one
    /// call: the last one counts.
    /// </summary>
    Task<UpsertResult> AddMissingAsync(IReadOnlyCollection<Video> videos, CancellationToken cancellationToken);

    Task<Video?> GetAsync(string id, CancellationToken cancellationToken);

    /// <returns>The video with its "file missing on the share" flag, or null when there's none with this id.</returns>
    Task<StoredVideo?> GetStoredAsync(string id, CancellationToken cancellationToken);

    Task<long> CountAsync(CancellationToken cancellationToken);

    /// <summary>Sets or clears the "file missing on the share" flag. Unknown ids are ignored.</summary>
    Task SetMediaMissingAsync(IReadOnlyCollection<string> ids, bool missing, CancellationToken cancellationToken);

    /// <summary>
    /// Records what the art store holds for these videos' thumbnails (resized versions, QA09). Unknown ids are
    /// ignored.
    /// </summary>
    Task SetThumbnailArtAsync(IReadOnlyDictionary<string, ArtVersions> art, CancellationToken cancellationToken);
}
