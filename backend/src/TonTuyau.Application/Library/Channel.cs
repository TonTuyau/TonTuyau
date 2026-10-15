using TonTuyau.Application.YouTube;

namespace TonTuyau.Application.Library;

/// <param name="ThumbPath">TubeArchivist cache path, or null when TA has none. Same for banner and TV art.</param>
/// <param name="Tabs">Which YouTube tabs the channel has: a subset of videos, shorts, streams.</param>
/// <param name="Active">TubeArchivist's flag: false when its last refresh of the channel failed (for any reason).</param>
/// <param name="YouTube">TonTuyau's own knowledge (1.5): null until the sync laid its facts over TubeArchivist's.</param>
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
    string? TvArtPath,
    YouTubeStatus? YouTube = null)
{
    /// <summary>The state as TonTuyau stands by it: its own, else from TubeArchivist's flag.</summary>
    public YouTubeStatus CurrentYouTube => YouTube ?? YouTubeStatus.FromTubeArchivist(Active);

    /// <summary>Whether visitors are told it's gone from YouTube: only when TonTuyau confirmed it.</summary>
    public bool GoneFromYouTube => CurrentYouTube.State == YouTubeState.Gone;
}
