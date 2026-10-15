using TonTuyau.Application.ChannelAdmin;
using TonTuyau.Application.YouTube;

namespace TonTuyau.Api.Contracts;

/// <summary>A channel's admin page (contract v1.12, TonTuyau 1.5).</summary>
/// <param name="Channel">The channel, as its card in the lists.</param>
/// <param name="YouTube">What TonTuyau knows of it on YouTube.</param>
/// <param name="Videos">How many of its videos are in each state on YouTube.</param>
public sealed record AdminChannelResponse(ChannelCard Channel, YouTubeStatusResponse YouTube, YouTubeStateCounts Videos)
{
    public static explicit operator AdminChannelResponse(ChannelAdminView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        IReadOnlyDictionary<YouTubeState, long> states = view.VideoStates;
        return new AdminChannelResponse(
            ApiMapper.ToDto(view.Card),
            (YouTubeStatusResponse)view.YouTube,
            new YouTubeStateCounts(
                states.GetValueOrDefault(YouTubeState.NotChecked),
                states.GetValueOrDefault(YouTubeState.OnYouTube),
                states.GetValueOrDefault(YouTubeState.RetryLater),
                states.GetValueOrDefault(YouTubeState.Gone)));
    }
}
