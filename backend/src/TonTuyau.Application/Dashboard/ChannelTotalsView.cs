using TonTuyau.Application.Library;
using TonTuyau.Application.Store;

namespace TonTuyau.Application.Dashboard;

/// <param name="Gone">Its videos no longer available on YouTube.</param>
/// <param name="Total">Videos, shorts and streams together.</param>
public sealed record ChannelTotalsView(
    string Id,
    string Name,
    ArtVersions? Thumbnail,
    TypeCounts Counts,
    long Gone,
    long Total);
