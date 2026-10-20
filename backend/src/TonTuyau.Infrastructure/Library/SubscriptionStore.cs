using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.Library;
using TonTuyau.Application.Store;
using TonTuyau.Infrastructure.Accounts;

namespace TonTuyau.Infrastructure.Library;

/// <summary>
/// The channels TonTuyau is subscribed to (2.0, B4), in PostgreSQL (<c>channel_subscriptions</c>). A singleton, a
/// database context per call, like <see cref="ChannelStore"/>.
/// </summary>
internal sealed class SubscriptionStore(IServiceScopeFactory scopes) : ISubscriptionStore
{
    public async Task<ChannelSubscription?> GetAsync(string channelId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(channelId);
        ChannelSubscriptionRow? row = await WithContextAsync(db => db.ChannelSubscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ChannelId == channelId, cancellationToken))
            .ConfigureAwait(false);
        return row is null ? null : ToSubscription(row);
    }

    public async Task<int> AddMissingAsync(
        IReadOnlyCollection<ChannelSubscription> subscriptions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subscriptions);
        ChannelSubscription[] distinct = subscriptions
            .DistinctBy(s => s.ChannelId, StringComparer.Ordinal)
            .ToArray();
        if (distinct.Length == 0)
        {
            return 0;
        }

        string[] ids = [.. distinct.Select(s => s.ChannelId)];
        return await WithContextAsync(async db =>
        {
            HashSet<string> existing = (await db.ChannelSubscriptions
                .Where(s => ids.Contains(s.ChannelId))
                .Select(s => s.ChannelId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
                .ToHashSet(StringComparer.Ordinal);
            ChannelSubscription[] missing = [.. distinct.Where(s => !existing.Contains(s.ChannelId))];
            db.ChannelSubscriptions.AddRange(missing.Select(ToRow));
            await db
                .SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);
            return missing.Length;
        })
            .ConfigureAwait(false);
    }

    public async Task<ChannelSubscription> SetAsync(
        ChannelSubscription subscription,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        return await WithContextAsync(async db =>
        {
            ChannelSubscriptionRow? row = await db.ChannelSubscriptions
                .FirstOrDefaultAsync(s => s.ChannelId == subscription.ChannelId, cancellationToken)
                .ConfigureAwait(false);
            if (row is null)
            {
                row = ToRow(subscription);
                db.ChannelSubscriptions.Add(row);
            }
            else
            {
                row.Videos = subscription.Tabs.Videos;
                row.Shorts = subscription.Tabs.Shorts;
                row.Streams = subscription.Tabs.Streams;
            }

            await db
                .SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);
            return ToSubscription(row);
        })
            .ConfigureAwait(false);
    }

    public async Task<bool> RemoveAsync(string channelId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(channelId);
        int removed = await WithContextAsync(db => db.ChannelSubscriptions
            .Where(s => s.ChannelId == channelId)
            .ExecuteDeleteAsync(cancellationToken))
            .ConfigureAwait(false);
        return removed > 0;
    }

    private static ChannelSubscriptionRow ToRow(ChannelSubscription subscription) => new()
    {
        ChannelId = subscription.ChannelId,
        Videos = subscription.Tabs.Videos,
        Shorts = subscription.Tabs.Shorts,
        Streams = subscription.Tabs.Streams,
        // PostgreSQL takes instants in UTC only.
        SubscribedAt = subscription.Since.ToUniversalTime(),
        SubscribedBy = subscription.By,
        Origin = subscription.Origin,
    };

    private static ChannelSubscription ToSubscription(ChannelSubscriptionRow row) =>
        new(
            row.ChannelId,
            new ChannelTabs(row.Videos, row.Shorts, row.Streams),
            row.SubscribedAt,
            row.SubscribedBy,
            row.Origin);

    private async Task<T> WithContextAsync<T>(Func<TonTuyauDbContext, Task<T>> work)
    {
        AsyncServiceScope scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            return await work(scope.ServiceProvider.GetRequiredService<TonTuyauDbContext>()).ConfigureAwait(false);
        }
    }
}
