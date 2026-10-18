using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.Library;
using TonTuyau.Application.Store;
using TonTuyau.Infrastructure.Accounts;

namespace TonTuyau.Infrastructure.Library;

/// <summary>The library's channels in PostgreSQL (2.0, <c>channels</c>). Same rules as <see cref="VideoStore"/>.</summary>
internal sealed class ChannelStore(IServiceScopeFactory scopes) : IChannelStore
{
    public async Task<UpsertResult> UpsertAsync(
        IReadOnlyCollection<Channel> channels,
        string? runId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channels);
        Channel[] distinct = channels
            .GroupBy(c => c.Id, StringComparer.Ordinal)
            .Select(g => g.Last())
            .ToArray();
        UpsertResult result = UpsertResult.None;
        foreach (Channel[] batch in distinct.Chunk(VideoStore.BatchSize))
        {
            result = result.Add(await UpsertBatchAsync(batch, runId, cancellationToken).ConfigureAwait(false));
        }

        return result;
    }

    public async Task<Channel?> GetAsync(string id, CancellationToken cancellationToken) =>
        await FindAsync(id, cancellationToken).ConfigureAwait(false) is { } row ? LibraryRows.ToChannel(row) : null;

    public async Task<StoredChannel?> GetStoredAsync(string id, CancellationToken cancellationToken) =>
        await FindAsync(id, cancellationToken).ConfigureAwait(false) is { } row ? LibraryRows.ToStored(row) : null;

    public Task<long> CountAsync(CancellationToken cancellationToken) =>
        WithContextAsync(db => db.Channels.LongCountAsync(cancellationToken));

    public async Task<int> DeleteAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        int deleted = 0;
        foreach (string[] batch in Batches(ids))
        {
            deleted += await WithContextAsync(db => db.Channels
                .Where(c => batch.Contains(c.Id))
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
            await WithContextAsync(db => db.Channels
                .Where(c => batch.Contains(c.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.SeenInRun, runId), cancellationToken))
                .ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<ChannelSummary>> FindNotSeenAsync(
        string runId,
        CancellationToken cancellationToken)
    {
        List<ChannelRow> rows = await WithContextAsync(db => db.Channels
            .AsNoTracking()
            .Where(c => c.SeenInRun == null || c.SeenInRun != runId)
            .ToListAsync(cancellationToken))
            .ConfigureAwait(false);
        return [.. rows.Select(LibraryRows.ToSummary)];
    }

    public async Task SetArtAsync(
        ArtSubject subject,
        IReadOnlyDictionary<string, ArtVersions> art,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(art);
        Action<ChannelRow, ArtVersionsRow> record = subject switch
        {
            ArtSubject.ChannelThumb => (row, versions) => row.ThumbArt = versions,
            ArtSubject.ChannelBanner => (row, versions) => row.BannerArt = versions,
            _ => throw new ArgumentOutOfRangeException(nameof(subject), subject, "Only channel thumbs and banners."),
        };
        foreach (string[] batch in Batches(art.Keys))
        {
            await WithContextAsync(async db =>
            {
                List<ChannelRow> rows = await db.Channels
                    .Where(c => batch.Contains(c.Id))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                foreach (ChannelRow row in rows)
                {
                    record(row, LibraryRows.ToRow(art[row.Id]));
                }

                return await db
                    .SaveChangesAsync(cancellationToken)
                    .ConfigureAwait(false);
            })
                .ConfigureAwait(false);
        }
    }

    private async Task<UpsertResult> UpsertBatchAsync(
        Channel[] batch,
        string? runId,
        CancellationToken cancellationToken)
    {
        string[] ids = [.. batch.Select(c => c.Id)];
        return await WithContextAsync(async db =>
        {
            Dictionary<string, ChannelRow> existing = await db.Channels
                .Where(c => ids.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, StringComparer.Ordinal, cancellationToken)
                .ConfigureAwait(false);
            int created = 0, updated = 0, unchanged = 0;
            List<string> changed = [];
            List<string> createdIds = [];
            foreach (Channel channel in batch)
            {
                string hash = LibraryRows.ContentHash(channel);
                if (existing.TryGetValue(channel.Id, out ChannelRow? row))
                {
                    if (row.ContentHash == hash)
                    {
                        unchanged++;
                    }
                    else
                    {
                        updated++;
                        changed.Add(channel.Id);
                        LibraryRows.WriteContent(row, channel, hash);
                    }
                }
                else
                {
                    created++;
                    changed.Add(channel.Id);
                    createdIds.Add(channel.Id);
                    row = LibraryRows.NewRow(channel, hash);
                    db.Channels.Add(row);
                }

                row.SeenInRun = runId ?? row.SeenInRun;
            }

            await db
                .SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);
            return new UpsertResult(created, updated, unchanged) { ChangedIds = changed, CreatedIds = createdIds };
        })
            .ConfigureAwait(false);
    }

    private Task<ChannelRow?> FindAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        return WithContextAsync(db => db.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken));
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
            .Chunk(VideoStore.BatchSize);
}
