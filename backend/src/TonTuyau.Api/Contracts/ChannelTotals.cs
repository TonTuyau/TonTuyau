using TonTuyau.Application.Browse;
using TonTuyau.Application.Dashboard;

namespace TonTuyau.Api.Contracts;

/// <summary>A channel in the dashboard's table.</summary>
/// <param name="Gone">Its videos TonTuyau found gone from YouTube.</param>
/// <param name="Private">Its videos made private by their owner (1.5, contract v1.16).</param>
/// <param name="Unlisted">Its videos on YouTube, not listed (1.5, contract v1.16).</param>
/// <param name="RetryLater">Its videos whose last check had no answer to trust (1.5).</param>
/// <param name="NotChecked">Its videos TonTuyau hasn't asked YouTube about yet (1.5).</param>
/// <param name="Total">Videos, shorts and streams together.</param>
public sealed record ChannelTotals(
    string Id,
    string Name,
    string? ThumbnailUrl,
    IReadOnlyList<ImageSize> Thumbnails,
    VideoCounts Counts,
    long Gone,
    long Private,
    long Unlisted,
    long RetryLater,
    long NotChecked,
    long Total)
{
    public static explicit operator ChannelTotals(ChannelTotalsView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        string thumbnail = ApiUrls.ChannelArt(view.Id, ChannelArtKind.Thumb);
        return new ChannelTotals(
            view.Id,
            view.Name,
            view.Thumbnail is null ? null : thumbnail,
            ApiMapper.Sizes(view.Thumbnail, thumbnail, w => ApiUrls.ChannelArt(view.Id, ChannelArtKind.Thumb, w)),
            new VideoCounts(view.Counts.Videos, view.Counts.Shorts, view.Counts.Streams),
            view.Gone,
            view.Private,
            view.Unlisted,
            view.RetryLater,
            view.NotChecked,
            view.Total);
    }
}
