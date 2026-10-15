using TonTuyau.Application.Browse;
using TonTuyau.Application.YouTube;

namespace TonTuyau.Application.ChannelAdmin;

/// <summary>A video of a channel's admin page (1.5): its card and what TonTuyau knows of it on YouTube.</summary>
public sealed record AdminVideoView(VideoCardView Card, YouTubeStatus YouTube);
