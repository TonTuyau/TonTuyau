using TonTuyau.Application.Library;

namespace TonTuyau.Application.Store;

/// <summary>
/// The library's videos as the sync keeps them (2.0): upsert by id, the marks of the run that saw them, the "file
/// missing" flag, the thumbnail's recorded versions. PostgreSQL holds them (<c>videos</c>), the source of truth;
/// <see cref="IVideoRepository"/> adds what the index answers (lists, search, counts).
/// </summary>
public interface IVideoStore
{
    /// <summary>
    /// Inserts or replaces by id. A video whose content didn't change isn't rewritten; when <paramref name="runId"/>
    /// is given, every video passed (changed or not) is marked as seen by that run.
    /// </summary>
    Task<UpsertResult> UpsertAsync(
        IReadOnlyCollection<Video> videos,
        string? runId,
        CancellationToken cancellationToken);

    Task<Video?> GetAsync(string id, CancellationToken cancellationToken);

    Task<long> CountAsync(CancellationToken cancellationToken);

    /// <returns>How many of the ids existed and were deleted.</returns>
    Task<int> DeleteAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken);

    /// <summary>Marks stored videos as seen by a run without rewriting them (e.g. a rejected document). Unknown ids are ignored.</summary>
    Task MarkSeenAsync(IReadOnlyCollection<string> ids, string runId, CancellationToken cancellationToken);

    /// <returns>Videos of these channels that the run didn't see.</returns>
    Task<IReadOnlyList<VideoSummary>> FindNotSeenAsync(
        string runId,
        IReadOnlyCollection<string> channelIds,
        CancellationToken cancellationToken);

    /// <returns>Every stored video of these channels.</returns>
    Task<IReadOnlyList<VideoSummary>> FindByChannelsAsync(
        IReadOnlyCollection<string> channelIds,
        CancellationToken cancellationToken);

    /// <returns>Ids of the videos currently flagged as having no file on the media share.</returns>
    Task<IReadOnlySet<string>> GetMediaMissingIdsAsync(CancellationToken cancellationToken);

    /// <summary>Sets or clears the "file missing on the share" flag. Not content: never changes the content hash.</summary>
    Task SetMediaMissingAsync(IReadOnlyCollection<string> ids, bool missing, CancellationToken cancellationToken);

    /// <summary>
    /// Records what the art store holds for these videos' thumbnails (resized versions, QA09). Not content: never
    /// changes the content hash, and a content rewrite keeps it. Unknown ids are ignored.
    /// </summary>
    Task SetThumbnailArtAsync(IReadOnlyDictionary<string, ArtVersions> art, CancellationToken cancellationToken);

    /// <returns>The video with its "file missing on the share" flag, or null when there's none with this id.</returns>
    Task<StoredVideo?> GetStoredAsync(string id, CancellationToken cancellationToken);
}
