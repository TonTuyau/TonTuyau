using TonTuyau.Application.Browse;
using TonTuyau.Application.Library;

namespace TonTuyau.Api.Contracts;

/// <summary>A channel's tabs (contract v2.5, the YouTube settings' default ones): each one required.</summary>
public sealed record ChannelTabsRequest(bool? Videos, bool? Shorts, bool? Streams)
{
    /// <exception cref="Application.Validation.ValidationException">A tab missing.</exception>
    public ChannelTabs ToTabs(string field)
    {
        if (Videos is null || Shorts is null || Streams is null)
        {
            throw RequestRules.Invalid(field, "needs videos, shorts and streams, each true or false.");
        }

        return new ChannelTabs(Videos.Value, Shorts.Value, Streams.Value);
    }
}
