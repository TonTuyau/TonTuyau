using TonTuyau.Application.YouTube;

namespace TonTuyau.Application.Library;

/// <param name="ThumbPath">
/// Art path (TubeArchivist's cache, or TonTuyau's art volume after a refresh); null: none. Same for banner and TV art.
/// </param>
/// <param name="YouTube">Its state on YouTube (1.5), as TonTuyau checked it: not checked until it does.</param>
public sealed record Channel(
    string Id,
    string Name,
    string Description,
    long Subscribers,
    IReadOnlyList<string> Tags,
    string? ThumbPath,
    string? BannerPath,
    string? TvArtPath,
    YouTubeStatus YouTube)
{
    /// <summary>Whether visitors are told it's gone from YouTube: only when TonTuyau confirmed it.</summary>
    public bool GoneFromYouTube => YouTube.State == YouTubeState.Gone;
}
