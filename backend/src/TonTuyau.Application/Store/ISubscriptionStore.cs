using TonTuyau.Application.Library;

namespace TonTuyau.Application.Store;

/// <summary>The channels TonTuyau is subscribed to (2.0, B4), in PostgreSQL (<c>channel_subscriptions</c>).</summary>
public interface ISubscriptionStore
{
    /// <returns>Its subscription; null when TonTuyau isn't subscribed to it (or has no such channel).</returns>
    Task<ChannelSubscription?> GetAsync(string channelId, CancellationToken cancellationToken);

    /// <summary>
    /// Adds the subscriptions TonTuyau doesn't have (the import: TubeArchivist's); one it has is left as it is. Their
    /// channels are in the library first (<c>channel_id</c> references <c>channels</c>).
    /// </summary>
    /// <returns>How many were added.</returns>
    Task<int> AddMissingAsync(
        IReadOnlyCollection<ChannelSubscription> subscriptions,
        CancellationToken cancellationToken);

    /// <summary>
    /// Subscribes to a channel of the library, or changes the tabs of its subscription: then when, by whom and where
    /// from it was subscribed stay as they were.
    /// </summary>
    /// <returns>The subscription as it is now.</returns>
    Task<ChannelSubscription> SetAsync(ChannelSubscription subscription, CancellationToken cancellationToken);

    /// <summary>Unsubscribes: the channel and its videos stay.</summary>
    /// <returns>False when TonTuyau wasn't subscribed to it.</returns>
    Task<bool> RemoveAsync(string channelId, CancellationToken cancellationToken);
}
