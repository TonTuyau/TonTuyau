using TonTuyau.Application.Library;
using TonTuyau.Application.Paging;
using TonTuyau.Application.Store;
using TonTuyau.Application.YouTube;
using TonTuyau.Infrastructure.Elasticsearch;

namespace TonTuyau.Infrastructure.Library;

/// <summary>
/// The library's videos (2.0): PostgreSQL, the source of truth, then the index's copy written again from it. Writes go
/// to PostgreSQL first, then whatever changed is indexed; a video's own reads (a channel's videos for its refresh)
/// read PostgreSQL; lists, search and counts read the index.
/// </summary>
internal sealed class LibraryVideoRepository(VideoStore store, VideoIndex index) : IVideoRepository
{
    public async Task<UpsertResult> AddMissingAsync(
        IReadOnlyCollection<Video> videos,
        CancellationToken cancellationToken)
    {
        UpsertResult added = await store.AddMissingAsync(videos, cancellationToken).ConfigureAwait(false);
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

        IReadOnlyList<StoredVideo> stored = await store.GetStoredAsync(ids, cancellationToken).ConfigureAwait(false);
        HashSet<string> found = stored
            .Select(s => s.Video.Id)
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

    public Task<Video?> GetAsync(string id, CancellationToken cancellationToken) =>
        store.GetAsync(id, cancellationToken);

    public Task<StoredVideo?> GetStoredAsync(string id, CancellationToken cancellationToken) =>
        index.GetStoredAsync(id, cancellationToken);

    public Task<long> CountAsync(CancellationToken cancellationToken) => store.CountAsync(cancellationToken);

    public Task<IReadOnlyList<VideoSummary>> FindByChannelsAsync(
        IReadOnlyCollection<string> channelIds,
        CancellationToken cancellationToken) =>
        store.FindByChannelsAsync(channelIds, cancellationToken);

    public async Task SetMediaMissingAsync(
        IReadOnlyCollection<string> ids,
        bool missing,
        CancellationToken cancellationToken)
    {
        await store.SetMediaMissingAsync(ids, missing, cancellationToken).ConfigureAwait(false);
        await index.SetMediaMissingAsync(ids, missing, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetThumbnailArtAsync(
        IReadOnlyDictionary<string, ArtVersions> art,
        CancellationToken cancellationToken)
    {
        await store.SetThumbnailArtAsync(art, cancellationToken).ConfigureAwait(false);
        await index.SetThumbnailArtAsync(art, cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<string>> GetIdsAfterAsync(
        string? after,
        int count,
        CancellationToken cancellationToken) =>
        store.GetIdsAfterAsync(after, count, cancellationToken);

    public Task<StorePage<VideoListItem>> FindAsync(
        VideoQuery query,
        PageRequest page,
        CancellationToken cancellationToken) =>
        index.FindAsync(query, page, cancellationToken);

    public Task<IReadOnlyDictionary<YouTubeState, long>> CountYouTubeStatesAsync(
        string channelId,
        CancellationToken cancellationToken) =>
        index.CountYouTubeStatesAsync(channelId, cancellationToken);

    public Task<IReadOnlyDictionary<string, TypeCounts>> CountByTypeAsync(
        IReadOnlyCollection<string> channelIds,
        CancellationToken cancellationToken) =>
        index.CountByTypeAsync(channelIds, cancellationToken);

    public Task<IReadOnlyList<TextMatch>> SuggestTitlesAsync(
        string prefix,
        int size,
        CancellationToken cancellationToken) =>
        index.SuggestTitlesAsync(prefix, size, cancellationToken);
}
