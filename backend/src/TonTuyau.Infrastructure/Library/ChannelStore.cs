using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.Library;
using TonTuyau.Application.Store;
using TonTuyau.Infrastructure.Accounts;

namespace TonTuyau.Infrastructure.Library;

/// <summary>The library's channels in PostgreSQL (2.0, <c>channels</c>). Same rules as <see cref="VideoStore"/>.</summary>
internal sealed class ChannelStore(IServiceScopeFactory scopes) : IChannelStore
{
    public async Task<UpsertResult> AddMissingAsync(
        IReadOnlyCollection<Channel> channels,
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
            result = result.Add(await AddBatchAsync(batch, cancellationToken).ConfigureAwait(false));
        }

        return result;
    }

    public async Task<Channel?> GetAsync(string id, CancellationToken cancellationToken) =>
        await FindAsync(id, cancellationToken).ConfigureAwait(false) is { } row ? LibraryRows.ToChannel(row) : null;

    public async Task<StoredChannel?> GetStoredAsync(string id, CancellationToken cancellationToken) =>
        await FindAsync(id, cancellationToken).ConfigureAwait(false) is { } row ? LibraryRows.ToStored(row) : null;

    public Task<long> CountAsync(CancellationToken cancellationToken) =>
        WithContextAsync(db => db.Channels.LongCountAsync(cancellationToken));

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

    private async Task<UpsertResult> AddBatchAsync(Channel[] batch, CancellationToken cancellationToken)
    {
        string[] ids = [.. batch.Select(c => c.Id)];
        return await WithContextAsync(async db =>
        {
            HashSet<string> existing = (await db.Channels
                .Where(c => ids.Contains(c.Id))
                .Select(c => c.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
                .ToHashSet(StringComparer.Ordinal);
            List<string> created = [];
            foreach (Channel channel in batch.Where(c => !existing.Contains(c.Id)))
            {
                db.Channels.Add(LibraryRows.NewRow(channel));
                created.Add(channel.Id);
            }

            await db
                .SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);
            return new UpsertResult(created.Count, 0, existing.Count) { ChangedIds = created, CreatedIds = created };
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
