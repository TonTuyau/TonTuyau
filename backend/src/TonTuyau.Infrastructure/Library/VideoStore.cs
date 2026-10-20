using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.Library;
using TonTuyau.Application.Store;
using TonTuyau.Infrastructure.Accounts;

namespace TonTuyau.Infrastructure.Library;

/// <summary>
/// The library's videos in PostgreSQL (2.0, <c>videos</c>): the source of truth. A singleton, a database context per
/// call; writes in batches.
/// </summary>
internal sealed class VideoStore(IServiceScopeFactory scopes) : IVideoStore
{
    public const int BatchSize = 500;

    public async Task<UpsertResult> AddMissingAsync(
        IReadOnlyCollection<Video> videos,
        LibraryOrigin origin,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(videos);
        Video[] distinct = videos
            .GroupBy(v => v.Id, StringComparer.Ordinal)
            .Select(g => g.Last())
            .ToArray();
        UpsertResult result = UpsertResult.None;
        foreach (Video[] batch in distinct.Chunk(BatchSize))
        {
            result = result.Add(await AddBatchAsync(batch, origin, cancellationToken).ConfigureAwait(false));
        }

        return result;
    }

    public async Task<Video?> GetAsync(string id, CancellationToken cancellationToken) =>
        await FindAsync(id, cancellationToken).ConfigureAwait(false) is { } row ? LibraryRows.ToVideo(row) : null;

    public async Task<StoredVideo?> GetStoredAsync(string id, CancellationToken cancellationToken) =>
        await FindAsync(id, cancellationToken).ConfigureAwait(false) is { } row ? LibraryRows.ToStored(row) : null;

    /// <returns>These videos with their flag and recorded versions, those the library has (to write the index).</returns>
    public async Task<IReadOnlyList<StoredVideo>> GetStoredAsync(
        IReadOnlyCollection<string> ids,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        List<StoredVideo> found = [];
        foreach (string[] batch in Batches(ids))
        {
            List<VideoRow> rows = await WithContextAsync(db => db.Videos
                .AsNoTracking()
                .Where(v => batch.Contains(v.Id))
                .ToListAsync(cancellationToken))
                .ConfigureAwait(false);
            found.AddRange(rows.Select(LibraryRows.ToStored));
        }

        return found;
    }

    public Task<long> CountAsync(CancellationToken cancellationToken) =>
        WithContextAsync(db => db.Videos.LongCountAsync(cancellationToken));

    public async Task<IReadOnlyList<VideoSummary>> FindByChannelsAsync(
        IReadOnlyCollection<string> channelIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channelIds);
        string[] channels = [.. channelIds];
        List<VideoSummary> found = await WithContextAsync(db => db.Videos
            .AsNoTracking()
            .IgnoreAutoIncludes()
            .Where(v => channels.Contains(v.ChannelId))
            .Select(v => new VideoSummary(v.Id, v.Title, v.ChannelId, v.Type, v.Downloaded, v.ThumbnailPath))
            .ToListAsync(cancellationToken))
            .ConfigureAwait(false);
        return found;
    }

    public async Task<IReadOnlyList<string>> GetIdsAfterAsync(
        string? after,
        int count,
        CancellationToken cancellationToken) =>
        await WithContextAsync(db => db.Videos
            .IgnoreAutoIncludes()
            .Where(v => after == null || v.Id.CompareTo(after) > 0)
            .OrderBy(v => v.Id)
            .Select(v => v.Id)
            .Take(count)
            .ToListAsync(cancellationToken))
            .ConfigureAwait(false);

    public async Task<bool> SetFileAsync(string id, VideoFile file, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentNullException.ThrowIfNull(file);
        return await WithContextAsync(async db =>
        {
            VideoRow? row = await db.Videos
                .FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
                .ConfigureAwait(false);
            if (row is null)
            {
                return false;
            }

            row.MediaPath = file.MediaPath;
            row.MediaPathCorrected = false;
            row.MediaSize = file.Size;
            row.Downloaded = file.Downloaded;
            row.Tracks = LibraryRows.ToRows(file.Tracks);
            row.MediaMissing = false;
            await db
                .SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);
            return true;
        })
            .ConfigureAwait(false);
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

    private async Task<UpsertResult> AddBatchAsync(
        Video[] batch,
        LibraryOrigin origin,
        CancellationToken cancellationToken)
    {
        string[] ids = [.. batch.Select(v => v.Id)];
        return await WithContextAsync(async db =>
        {
            HashSet<string> existing = (await db.Videos
                .Where(v => ids.Contains(v.Id))
                .Select(v => v.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
                .ToHashSet(StringComparer.Ordinal);
            // One run at a time (the run lock): nobody adds the same video, or category, meanwhile.
            Video[] missing = [.. batch.Where(v => !existing.Contains(v.Id))];
            Dictionary<string, CategoryRow> categories = await LibraryCategories
                .FindOrAddAsync(db, missing.Select(v => v.Categories), cancellationToken)
                .ConfigureAwait(false);
            List<string> created = [];
            foreach (Video video in missing)
            {
                db.Videos.Add(LibraryRows.NewRow(video, origin, LibraryCategories.Of(video.Categories, categories)));
                created.Add(video.Id);
            }

            await db
                .SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);
            return new UpsertResult(created.Count, 0, existing.Count) { ChangedIds = created, CreatedIds = created };
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
