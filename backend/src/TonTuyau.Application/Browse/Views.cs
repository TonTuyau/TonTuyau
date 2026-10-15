using TonTuyau.Application.Library;
using TonTuyau.Application.Store;
using TonTuyau.Application.YouTube;

namespace TonTuyau.Application.Browse;

// What the services answer. The API layer turns these into the contract's JSON shapes and builds the URLs. An
// image is its versions (the original's width and the resized widths that exist), or null when there's no image
// to point to (no URL is handed out for a missing one).

/// <param name="Subscribers">0 when the channel isn't in the store (only the watch page shows it).</param>
public sealed record ChannelRefView(string Id, string Name, ArtVersions? Thumbnail, long Subscribers);

/// <param name="Active">False only when TonTuyau confirmed the video is gone from YouTube (1.5).</param>
public sealed record VideoCardView(
    string Id,
    string Title,
    VideoType Type,
    DateTimeOffset Published,
    TimeSpan Duration,
    long Views,
    bool Active,
    ArtVersions? Thumbnail,
    ChannelRefView Channel);

/// <param name="MimeType">What the front asks the browser about (see <see cref="Media.MediaTypes.PlaybackType"/>).</param>
/// <param name="MediaAvailable">False when the full sync found the file missing on the share.</param>
public sealed record VideoDetailView(
    VideoCardView Card,
    string Description,
    DateTimeOffset Downloaded,
    long Likes,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Categories,
    long FileSize,
    string MimeType,
    IReadOnlyList<MediaTrack> Tracks,
    bool MediaAvailable,
    IReadOnlyList<YouTubeChapter> Chapters);

public sealed record ChannelCardView(
    string Id,
    string Name,
    long Subscribers,
    ArtVersions? Thumbnail,
    TypeCounts VideoCounts);

/// <param name="Active">False only when TonTuyau confirmed the channel is gone from YouTube (1.5).</param>
public sealed record ChannelDetailView(
    ChannelCardView Card,
    string Description,
    ArtVersions? Banner,
    IReadOnlyList<string> Tags,
    bool Active);

public sealed record HomeSectionView(VideoType Type, IReadOnlyList<VideoCardView> Items);

public enum SuggestionKind
{
    Channel,
    Video,
}

public sealed record SuggestionView(string Text, SuggestionKind Kind, string Id);

/// <summary>A search's answer. Which parts are set depends on <see cref="Kind"/>.</summary>
/// <param name="TopChannels">kind=all: the best channel matches, on the first page only (empty after).</param>
/// <param name="Videos">kind=all or videos.</param>
/// <param name="Channels">kind=channels.</param>
public sealed record SearchResultsView(
    SearchKind Kind,
    IReadOnlyList<ChannelCardView>? TopChannels,
    Paging.ResultPage<VideoCardView>? Videos,
    Paging.ResultPage<ChannelCardView>? Channels);
