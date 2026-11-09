using TonTuyau.Application.ChannelAdmin;
using TonTuyau.Application.YouTube;

namespace TonTuyau.Api.Contracts;

/// <summary>A channel's admin page (contract v1.12, TonTuyau 1.5).</summary>
/// <param name="Channel">The channel, as its card in the lists.</param>
/// <param name="YouTube">What TonTuyau knows of it on YouTube.</param>
/// <param name="Videos">How many of its videos are in each state on YouTube.</param>
/// <param name="Subscription">TonTuyau's subscription to it (contract v2.3); null: not subscribed.</param>
/// <param name="Size">Its files' sizes by type, in bytes (contract v2.12).</param>
/// <param name="SyncBlocked">
/// How many of its videos have their syncs blocked (contract v2.17), counted in <paramref name="Videos"/> too.
/// </param>
public sealed record AdminChannelResponse(
    ChannelCard Channel,
    YouTubeStatusResponse YouTube,
    YouTubeStateCounts Videos,
    SubscriptionResponse? Subscription,
    VideoCounts Size,
    long SyncBlocked)
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
                states.GetValueOrDefault(YouTubeState.Unlisted),
                states.GetValueOrDefault(YouTubeState.Private),
                states.GetValueOrDefault(YouTubeState.RetryLater),
                states.GetValueOrDefault(YouTubeState.Gone)),
            view.Subscription is null ? null : (SubscriptionResponse)view.Subscription,
            new VideoCounts(view.Sizes.Videos, view.Sizes.Shorts, view.Sizes.Streams),
            view.SyncBlocked);
    }
}
