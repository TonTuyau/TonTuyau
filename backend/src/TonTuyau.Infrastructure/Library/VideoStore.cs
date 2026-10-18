using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.Library;
using TonTuyau.Application.Store;
using TonTuyau.Infrastructure.Accounts;

namespace TonTuyau.Infrastructure.Library;

/// <summary>
/// The library's videos in PostgreSQL (2.0, <c>videos</c>): the source of truth. A singleton like the sync, a
/// database context per call; writes in batches.
/// </summary>
internal sealed class VideoStore(IServiceScopeFactory scopes) : IVideoStore
{
    public const int BatchSize = 500;

    public async Task<UpsertResult> UpsertAsync(
        IReadOnlyCollection<Video> videos,
        string? runId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(videos);

        // Same id twice in one call: the last one wins, like successive upserts would.
        Video[] distinct = videos
            .GroupBy(v => v.Id, StringComparer.Ordinal)
            .Select(g => g.Last())
            .ToArray();
        UpsertResult result = UpsertResult.None;
        foreach (Video[] batch in distinct.Chunk(BatchSize))
        {
            result = result.Add(await UpsertBatchAsync(batch, runId, cancellationToken).ConfigureAwait(false));
        }

        return result;
    }

    public async Task<Video?> GetAsync(string id, CancellationToken cancellationToken) =>
        await FindAsync(id, cancellationToken).ConfigureAwait(false) is { } row ? LibraryRows.ToVideo(row) : null;

    public async Task<StoredVideo?> GetStoredAsync(string id, CancellationToken cancellationToken) =>
        await FindAsync(id, cancellationToken).ConfigureAwait(false) is { } row ? LibraryRows.ToStored(row) : null;

    public Task<long> CountAsync(CancellationToken cancellationToken) =>
        WithContextAsync(db => db.Videos.LongCountAsync(cancellationToken));

    public async Task<int> DeleteAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        int deleted = 0;
        foreach (string[] batch in Batches(ids))
        {
            deleted += await WithContextAsync(db => db.Videos
                .Where(v => batch.Contains(v.Id))
                .ExecuteDeleteAsync(cancellationToken))
                .ConfigureAwait(false);
        }

        return deleted;
    }

    public async Task MarkSeenAsync(IReadOnlyCollection<string> ids, string runId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        foreach (string[] batch in Batches(ids))
        {
            await WithContextAsync(db => db.Videos
                .Where(v => batch.Contains(v.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.SeenInRun, runId), cancellationToken))
                .ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<VideoSummary>> FindNotSeenAsync(
        string runId,
        IReadOnlyCollection<string> channelIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channelIds);
        string[] channels = [.. channelIds];
        List<VideoRow> rows = await WithContextAsync(db => db.Videos
            .AsNoTracking()
            .Where(v => channels.Contains(v.ChannelId) && (v.SeenInRun == null || v.SeenInRun != runId))
            .ToListAsync(cancellationToken))
            .ConfigureAwait(false);
        return [.. rows.Select(LibraryRows.ToSummary)];
    }

    public async Task<IReadOnlyList<VideoSummary>> FindByChannelsAsync(
        IReadOnlyCollection<string> channelIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channelIds);
        string[] channels = [.. channelIds];
        List<VideoRow> rows = await WithContextAsync(db => db.Videos
            .AsNoTracking()
            .Where(v => channels.Contains(v.ChannelId))
            .ToListAsync(cancellationToken))
            .ConfigureAwait(false);
        return [.. rows.Select(LibraryRows.ToSummary)];
    }

    public async Task<IReadOnlySet<string>> GetMediaMissingIdsAsync(CancellationToken cancellationToken)
    {
        List<string> ids = await WithContextAsync(db => db.Videos
            .Where(v => v.MediaMissing)
            .Select(v => v.Id)
            .ToListAsync(cancellationToken))
            .ConfigureAwait(false);
        return ids.ToHashSet(StringComparer.Ordinal);
    }

    public async Task SetMediaMissingAsync(
        IReadOnlyCollection<string> ids,
        bool missing,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        foreach (string[] batch in Batches(ids))
        {
            await WithContextAsync(db => db.Videos
                .Where(v => batch.Contains(v.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.MediaMissing, missing), cancellationToken))
                .ConfigureAwait(false);
        }
    }

    public async Task SetThumbnailArtAsync(
        IReadOnlyDictionary<string, ArtVersions> art,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(art);
        foreach (string[] batch in Batches(art.Keys))
        {
            await WithContextAsync(async db =>
            {
                List<VideoRow> rows = await db.Videos
                    .Where(v => batch.Contains(v.Id))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                foreach (VideoRow row in rows)
                {
                    row.ThumbnailArt = LibraryRows.ToRow(art[row.Id]);
                }

                return await db
                    .SaveChangesAsync(cancellationToken)
                    .ConfigureAwait(false);
            })
                .ConfigureAwait(false);
        }
    }

    private async Task<UpsertResult> UpsertBatchAsync(
        Video[] batch,
        string? runId,
        CancellationToken cancellationToken)
    {
        string[] ids = [.. batch.Select(v => v.Id)];
        return await WithContextAsync(async db =>
        {
            Dictionary<string, VideoRow> existing = await db.Videos
                .Where(v => ids.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id, StringComparer.Ordinal, cancellationToken)
                .ConfigureAwait(false);
            int created = 0, updated = 0, unchanged = 0;
            List<string> changed = [];
            List<string> createdIds = [];
            foreach (Video video in batch)
            {
                string hash = LibraryRows.ContentHash(video);
                if (existing.TryGetValue(video.Id, out VideoRow? row))
                {
                    if (row.ContentHash == hash)
                    {
                        unchanged++;
                    }
                    else
                    {
                        updated++;
                        changed.Add(video.Id);
                        // Only TubeArchivist's columns: the bookkeeping and TonTuyau's own facts stay.
                        LibraryRows.WriteContent(row, video, hash);
                    }
                }
                else
                {
                    created++;
                    changed.Add(video.Id);
                    createdIds.Add(video.Id);
                    row = LibraryRows.NewRow(video, hash);
                    db.Videos.Add(row);
                }

                // A run's id replaces the old "seen" mark; a plain write keeps it.
                row.SeenInRun = runId ?? row.SeenInRun;
            }

            await db
                .SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);
            return new UpsertResult(created, updated, unchanged) { ChangedIds = changed, CreatedIds = createdIds };
        })
            .ConfigureAwait(false);
    }

    private Task<VideoRow?> FindAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        return WithContextAsync(db => db.Videos
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken));
    }

    private async Task<T> WithContextAsync<T>(Func<TonTuyauDbContext, Task<T>> work)
    {
        AsyncServiceScope scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            return await work(scope.ServiceProvider.GetRequiredService<TonTuyauDbContext>()).ConfigureAwait(false);
        }
    }

    private static IEnumerable<string[]> Batches(IEnumerable<string> ids) =>
        ids
            .Distinct(StringComparer.Ordinal)
            .Chunk(BatchSize);
}
