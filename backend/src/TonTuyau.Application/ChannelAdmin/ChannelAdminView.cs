using TonTuyau.Application.Browse;
using TonTuyau.Application.YouTube;

namespace TonTuyau.Application.ChannelAdmin;

/// <summary>A channel's admin page (1.5): the channel, what TonTuyau knows of it on YouTube, its videos' states.</summary>
/// <param name="VideoStates">How many of its videos are in each state on YouTube (every state, 0 included).</param>
public sealed record ChannelAdminView(
    ChannelCardView Card,
    YouTubeStatus YouTube,
    IReadOnlyDictionary<YouTubeState, long> VideoStates);
