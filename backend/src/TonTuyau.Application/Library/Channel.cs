namespace TonTuyau.Application.Library;

/// <param name="ThumbPath">TubeArchivist cache path, or null when TA has none. Same for banner and TV art.</param>
/// <param name="Tabs">Which YouTube tabs the channel has: a subset of videos, shorts, streams.</param>
public sealed record Channel(
    string Id,
    string Name,
    string Description,
    long Subscribers,
    bool Active,
    DateTimeOffset? LastRefreshed,
    IReadOnlyList<string> Tags,
    IReadOnlyList<VideoType> Tabs,
    string? ThumbPath,
    string? BannerPath,
    string? TvArtPath);
