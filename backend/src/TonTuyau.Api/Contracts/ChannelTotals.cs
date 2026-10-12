using TonTuyau.Application.Browse;
using TonTuyau.Application.Dashboard;

namespace TonTuyau.Api.Contracts;

/// <summary>A channel in the dashboard's table.</summary>
/// <param name="Gone">Its videos no longer available on YouTube.</param>
/// <param name="Total">Videos, shorts and streams together.</param>
public sealed record ChannelTotals(
    string Id,
    string Name,
    string? ThumbnailUrl,
    IReadOnlyList<ImageSize> Thumbnails,
    VideoCounts Counts,
    long Gone,
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
            view.Total);
    }
}
