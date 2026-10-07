using TonTuyau.Application.Library;

namespace TonTuyau.Application.TubeArchivist;

/// <summary>The sort keys TubeArchivist v0.5.12 accepts (it rejects <c>filesize</c>, despite its docs).</summary>
public enum VideoSort
{
    Published,
    Downloaded,
    Views,
    Likes,
    Duration,
}

public enum SortOrder
{
    Asc,
    Desc,
}

/// <param name="ChannelId">Only this channel's videos, or all when null.</param>
/// <param name="Type">Only this type, or all when null.</param>
public sealed record VideoPageQuery(VideoSort Sort, SortOrder Order, string? ChannelId = null, VideoType? Type = null);
