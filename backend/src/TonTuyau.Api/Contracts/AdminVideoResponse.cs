using TonTuyau.Application.ChannelAdmin;

namespace TonTuyau.Api.Contracts;

/// <summary>
/// A video of a channel's admin page (contract v1.12, TonTuyau 1.5): its card and its state on YouTube; whether its syncs
/// are blocked (contract v2.17: its file purged on its own, out of the library's lists).
/// </summary>
public sealed record AdminVideoResponse(VideoCard Video, YouTubeStatusResponse YouTube, bool SyncBlocked)
{
    public static explicit operator AdminVideoResponse(AdminVideoView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new AdminVideoResponse(
            ApiMapper.ToDto(view.Card),
            (YouTubeStatusResponse)view.YouTube,
            view.SyncBlocked);
    }
}
