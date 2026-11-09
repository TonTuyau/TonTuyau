using TonTuyau.Application.Library;
using TonTuyau.Application.Paging;
using TonTuyau.Application.Store;
using TonTuyau.Application.YouTube;
using TonTuyau.Infrastructure.Elasticsearch;

namespace TonTuyau.Infrastructure.Library;

/// <summary>
/// The library's videos (2.0): PostgreSQL, the source of truth, then the index's copy written again from it. Writes go
/// to PostgreSQL first, then whatever changed is indexed; a video on its own (its page, its file) and a channel's
/// videos (for its refresh) read PostgreSQL; lists, search and counts read the index.
/// </summary>
internal sealed class LibraryVideoRepository(VideoStore store, VideoIndex index) : IVideoRepository
{
    public async Task<UpsertResult> AddMissingAsync(
        IReadOnlyCollection<Video> videos,
        LibraryOrigin origin,
        CancellationToken cancellationToken)
    {
        UpsertResult added = await store.AddMissingAsync(videos, origin, cancellationToken).ConfigureAwait(false);
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

        // A video whose file was purged on its own (2.3.0) is left out, as one PostgreSQL doesn't have.
        IReadOnlyList<StoredVideo> stored = [
            .. (await store.GetStoredAsync(ids, cancellationToken).ConfigureAwait(false)).Where(s => !s.SyncBlocked),
        ];
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
        store.GetStoredAsync(id, cancellationToken);

    public Task<long> CountAsync(CancellationToken cancellationToken) => store.CountAsync(cancellationToken);

    public Task<IReadOnlyList<VideoSummary>> FindByChannelsAsync(
        IReadOnlyCollection<string> channelIds,
        CancellationToken cancellationToken) =>
        store.FindByChannelsAsync(channelIds, cancellationToken);

    public async Task<bool> SetFileAsync(string id, VideoFile file, CancellationToken cancellationToken)
    {
        if (!await store.SetFileAsync(id, file, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        await ReindexAsync([id], cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<int> SetSyncBlockedAsync(
        IReadOnlyCollection<string> ids,
        bool blocked,
        CancellationToken cancellationToken)
    {
        int changed = await store.SetSyncBlockedAsync(ids, blocked, cancellationToken).ConfigureAwait(false);
        // Blocked: they leave the index; unblocked: they're back in it.
        await ReindexAsync(ids, cancellationToken).ConfigureAwait(false);
        return changed;
    }

    public Task<IReadOnlyList<StoredVideo>> ListSyncBlockedAsync(
        string channelId,
        CancellationToken cancellationToken) =>
        store.ListSyncBlockedAsync(channelId, cancellationToken);

    public Task<IReadOnlyList<VideoFileState>> ListFilesAsync(CancellationToken cancellationToken) =>
        store.ListFilesAsync(cancellationToken);

    public async Task<int> DeleteAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken)
    {
        int deleted = await store.DeleteAsync(ids, cancellationToken).ConfigureAwait(false);
        // PostgreSQL doesn't have them any more: they leave the index.
        await ReindexAsync(ids, cancellationToken).ConfigureAwait(false);
        return deleted;
    }

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
