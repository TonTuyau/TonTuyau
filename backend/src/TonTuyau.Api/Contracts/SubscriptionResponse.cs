using TonTuyau.Application.Library;

namespace TonTuyau.Api.Contracts;

/// <summary>TonTuyau's subscription to a channel (contract v2.3): the YouTube syncs download its videos.</summary>
/// <param name="Tabs">Which of its tabs: videos, shorts, streams.</param>
/// <param name="Since">When it was subscribed (an import: when it took it from TubeArchivist).</param>
/// <param name="By">The account that subscribed it; null: not an account (the import, the command line).</param>
/// <param name="Origin">tubeArchivist (taken from it at the import) | tonTuyau.</param>
public sealed record SubscriptionResponse(ChannelTabsResponse Tabs, DateTime Since, string? By, LibraryOrigin Origin)
{
    public static explicit operator SubscriptionResponse(ChannelSubscription subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        return new SubscriptionResponse(
            (ChannelTabsResponse)subscription.Tabs,
            subscription.Since.UtcDateTime,
            subscription.By,
            subscription.Origin);
    }
}
