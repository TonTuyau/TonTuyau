using TonTuyau.Application.Library;

namespace TonTuyau.Application.Store;

/// <summary>A stored video in short: what a channel's refresh orders and names its videos by.</summary>
public sealed record VideoSummary(
    string Id,
    string Title,
    string ChannelId,
    VideoType Type,
    DateTimeOffset Downloaded,
    string ThumbnailPath);
