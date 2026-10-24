using TonTuyau.Application.Library;
using TonTuyau.Application.Store;

namespace TonTuyau.Application.Dashboard;

/// <param name="Gone">Its videos TonTuyau found gone from YouTube.</param>
/// <param name="Private">Its videos made private by their owner (1.5).</param>
/// <param name="Unlisted">Its videos on YouTube, not listed (1.5).</param>
/// <param name="RetryLater">Its videos whose last check had no answer to trust (1.5).</param>
/// <param name="NotChecked">Its videos TonTuyau hasn't asked YouTube about yet (1.5).</param>
/// <param name="Total">Videos, shorts and streams together.</param>
/// <param name="Size">Its files' sizes together, in bytes (2.2.0).</param>
public sealed record ChannelTotalsView(
    string Id,
    string Name,
    ArtVersions? Thumbnail,
    TypeCounts Counts,
    long Gone,
    long Private,
    long Unlisted,
    long RetryLater,
    long NotChecked,
    long Total,
    long Size = 0);
