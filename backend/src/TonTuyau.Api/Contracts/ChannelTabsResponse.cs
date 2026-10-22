using TonTuyau.Application.Library;

namespace TonTuyau.Api.Contracts;

/// <summary>Which of a channel's tabs on YouTube TonTuyau downloads (contract v2.3): videos, shorts, streams.</summary>
public sealed record ChannelTabsResponse(bool Videos, bool Shorts, bool Streams)
{
    public static explicit operator ChannelTabsResponse(ChannelTabs tabs)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        return new ChannelTabsResponse(tabs.Videos, tabs.Shorts, tabs.Streams);
    }
}
