using TonTuyau.Application.Browse;
using TonTuyau.Application.YouTube;

namespace TonTuyau.Application.ChannelAdmin;

/// <summary>
/// A video of a channel's admin page (1.5): its card and what TonTuyau knows of it on YouTube; whether its syncs are
/// blocked (2.3.0, its file purged on its own).
/// </summary>
public sealed record AdminVideoView(VideoCardView Card, YouTubeStatus YouTube, bool SyncBlocked = false);
