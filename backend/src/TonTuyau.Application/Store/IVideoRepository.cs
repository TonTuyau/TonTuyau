using TonTuyau.Application.Library;
using TonTuyau.Application.Paging;

namespace TonTuyau.Application.Store;

/// <summary>TonTuyau's own copy of the videos (Elasticsearch). Writes go to the write alias, reads to the read alias.</summary>
public interface IVideoRepository
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

    // Sync support. These work on the write alias: during a rebuild, that's the generation being built.

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

    // Browsing and search (the REST API). These read the read alias: what users see.

    /// <returns>The video with its "file missing on the share" flag, or null when there's none with this id.</returns>
    Task<StoredVideo?> GetStoredAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// One page of videos matching the query, sorted, with the exact total. Pages follow each other with
    /// <see cref="PageRequest.After"/> (<c>search_after</c>), so there's no depth limit.
    /// </summary>
    /// <exception cref="Validation.ValidationException">The position doesn't fit the query's sort.</exception>
    Task<StorePage<VideoListItem>> FindAsync(VideoQuery query, PageRequest page, CancellationToken cancellationToken);

    /// <returns>Per channel id: how many videos, shorts and streams it has. Channels without any are left out.</returns>
    Task<IReadOnlyDictionary<string, TypeCounts>> CountByTypeAsync(
        IReadOnlyCollection<string> channelIds,
        CancellationToken cancellationToken);

    /// <returns>Videos whose title starts words with what was typed (accents and case ignored), most viewed first.</returns>
    Task<IReadOnlyList<TextMatch>> SuggestTitlesAsync(string prefix, int size, CancellationToken cancellationToken);
}
