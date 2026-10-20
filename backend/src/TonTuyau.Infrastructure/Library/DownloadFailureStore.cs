using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.Library;
using TonTuyau.Application.Store;
using TonTuyau.Infrastructure.Accounts;

namespace TonTuyau.Infrastructure.Library;

/// <summary>
/// The videos the YouTube syncs couldn't download (2.0, B5), in PostgreSQL (<c>download_failures</c>). A singleton, a
/// database context per call, like <see cref="SubscriptionStore"/>.
/// </summary>
internal sealed class DownloadFailureStore(IServiceScopeFactory scopes) : IDownloadFailureStore
{
    public async Task<DownloadFailure> RecordAsync(
        string videoId,
        string channelId,
        string? title,
        string reason,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(videoId);
        ArgumentException.ThrowIfNullOrEmpty(channelId);
        DateTimeOffset utc = at.ToUniversalTime();
        return await WithContextAsync(async db =>
        {
            DownloadFailureRow? row = await db.DownloadFailures
                .FirstOrDefaultAsync(f => f.VideoId == videoId, cancellationToken)
                .ConfigureAwait(false);
            if (row is null)
            {
                row = new DownloadFailureRow
                {
                    VideoId = videoId,
                    ChannelId = channelId,
                    Reason = reason,
                    FirstFailedAt = utc,
                };
                db.DownloadFailures.Add(row);
            }

            row.Title = title ?? row.Title;
            row.Reason = reason;
            row.Tries++;
            row.LastFailedAt = utc;
            await db
                .SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);
            return ToFailure(row);
        })
            .ConfigureAwait(false);
    }

    public async Task<DownloadFailure?> GetAsync(string videoId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(videoId);
        DownloadFailureRow? row = await WithContextAsync(db => db.DownloadFailures
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.VideoId == videoId, cancellationToken))
            .ConfigureAwait(false);
        return row is null ? null : ToFailure(row);
    }

    public async Task<IReadOnlyList<DownloadFailure>> ListAsync(
        string channelId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(channelId);
        List<DownloadFailureRow> rows = await WithContextAsync(db => db.DownloadFailures
            .AsNoTracking()
            .Where(f => f.ChannelId == channelId)
            .OrderByDescending(f => f.LastFailedAt)
            .ThenBy(f => f.VideoId)
            .ToListAsync(cancellationToken))
            .ConfigureAwait(false);
        return [.. rows.Select(ToFailure)];
    }

    public async Task RemoveAsync(string videoId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(videoId);
        await WithContextAsync(db => db.DownloadFailures
            .Where(f => f.VideoId == videoId)
            .ExecuteDeleteAsync(cancellationToken))
            .ConfigureAwait(false);
    }

    private static DownloadFailure ToFailure(DownloadFailureRow row) =>
        new(row.VideoId, row.ChannelId, row.Title, row.Reason, row.Tries, row.FirstFailedAt, row.LastFailedAt);

    private async Task<T> WithContextAsync<T>(Func<TonTuyauDbContext, Task<T>> work)
    {
        AsyncServiceScope scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            return await work(scope.ServiceProvider.GetRequiredService<TonTuyauDbContext>()).ConfigureAwait(false);
        }
    }
}
