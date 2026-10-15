using System.Text.Json.Serialization;
using TonTuyau.Application.Browse;
using TonTuyau.Application.Library;

namespace TonTuyau.Api.Contracts;

// The JSON shapes of API_CONTRACT.md. Class names are the OpenAPI schema names; properties are camelCase on the
// wire, dates are UTC (DateTime of kind Utc → "2026-10-02T20:18:48Z"), enums are lower-case strings. Card properties
// come first in the ChannelDetail JSON (order -1), the detail's after them.

/// <summary>
/// One version of an image (contract v1.4): its width and URL. Lists are sorted by width, the original last with its
/// real width; only versions that exist are listed; no image = an empty list.
/// </summary>
public sealed record ImageSize(int Width, string Url);

/// <summary>The channel of a <see cref="VideoCard"/>.</summary>
public sealed record VideoChannel(string Id, string Name, string? ThumbnailUrl, IReadOnlyList<ImageSize> Thumbnails);

/// <summary>A video in a list.</summary>
public sealed class VideoCard
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required VideoType Type { get; init; }

    public required DateTime Published { get; init; }

    public required long DurationSeconds { get; init; }

    public required long Views { get; init; }

    /// <summary>False: removed from YouTube, still archived (badge, R2-9).</summary>
    public required bool Active { get; init; }

    /// <summary>Null when there's no image.</summary>
    public required string? ThumbnailUrl { get; init; }

    /// <summary>The thumbnail's versions, for <c>srcset</c> (contract v1.4).</summary>
    public required IReadOnlyList<ImageSize> Thumbnails { get; init; }

    public required VideoChannel Channel { get; init; }
}

/// <summary>One stream of the media file. Width and height only for video tracks.</summary>
public sealed record Track(MediaTrackKind Kind, string Codec, int? Width, int? Height, long Bitrate);

/// <summary>The channel of a <see cref="VideoDetail"/>: a <see cref="VideoChannel"/> plus its subscribers.</summary>
public sealed record VideoDetailChannel(
    string Id,
    string Name,
    string? ThumbnailUrl,
    IReadOnlyList<ImageSize> Thumbnails,
    long Subscribers);

/// <summary>
/// The watch page: the <see cref="VideoCard"/> fields (its channel with subscribers, contract v1.1) plus everything
/// about the file. Not derived from <see cref="VideoCard"/>, whose channel stays without subscribers.
/// </summary>
public sealed class VideoDetail
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required VideoType Type { get; init; }

    public required DateTime Published { get; init; }

    public required long DurationSeconds { get; init; }

    public required long Views { get; init; }

    public required bool Active { get; init; }

    public required string? ThumbnailUrl { get; init; }

    public required IReadOnlyList<ImageSize> Thumbnails { get; init; }

    public required VideoDetailChannel Channel { get; init; }

    public required string Description { get; init; }

    public required DateTime Downloaded { get; init; }

    public required long Likes { get; init; }

    public required IReadOnlyList<string> Tags { get; init; }

    public required IReadOnlyList<string> Categories { get; init; }

    public required long FileSize { get; init; }

    /// <summary>For <c>canPlayType()</c>: e.g. <c>video/mp4; codecs="av01.0.08M.08, opus"</c>.</summary>
    public required string MimeType { get; init; }

    public required IReadOnlyList<Track> Tracks { get; init; }

    public required string MediaUrl { get; init; }

    public required string DownloadUrl { get; init; }

    /// <summary>False when the full sync found the file missing on the share.</summary>
    public required bool MediaAvailable { get; init; }

    /// <summary>Its chapters, in order (1.5, contract v1.13): none until TonTuyau refreshed it, or when YouTube marks none.</summary>
    public required IReadOnlyList<Chapter> Chapters { get; init; }
}

/// <summary>A chapter of a video: from where to where, in seconds, and its title (contract v1.13).</summary>
public sealed record Chapter(double StartSeconds, double EndSeconds, string Title);

/// <summary>How many videos of each type a channel has (its tabs = the types above 0).</summary>
public sealed record VideoCounts(long Videos, long Shorts, long Streams);

/// <summary>A channel in a list.</summary>
public class ChannelCard
{
    [JsonPropertyOrder(-1)]
    public required string Id { get; init; }

    [JsonPropertyOrder(-1)]
    public required string Name { get; init; }

    [JsonPropertyOrder(-1)]
    public required long Subscribers { get; init; }

    [JsonPropertyOrder(-1)]
    public required string? ThumbnailUrl { get; init; }

    [JsonPropertyOrder(-1)]
    public required IReadOnlyList<ImageSize> Thumbnails { get; init; }

    [JsonPropertyOrder(-1)]
    public required VideoCounts VideoCounts { get; init; }
}

/// <summary>A channel's page: a <see cref="ChannelCard"/> plus its description and art.</summary>
public sealed class ChannelDetail : ChannelCard
{
    public required string Description { get; init; }

    public required string? BannerUrl { get; init; }

    public required IReadOnlyList<ImageSize> Banners { get; init; }

    public required IReadOnlyList<string> Tags { get; init; }

    public required bool Active { get; init; }
}

/// <summary>A cursor-paged list. <see cref="NextCursor"/> is null on the last page; <see cref="Total"/> is exact.</summary>
public sealed record Page<T>(IReadOnlyList<T> Items, string? NextCursor, long Total);

public sealed record HomeSection(VideoType Type, IReadOnlyList<VideoCard> Items);

/// <summary>Latest published first; sections in the order video, short, stream; empty sections left out.</summary>
public sealed record HomeResponse(IReadOnlyList<HomeSection> Sections);

/// <summary><c>/api/search?kind=all</c>: up to 3 channels (first page only) and a page of videos.</summary>
public sealed record SearchAllResponse(IReadOnlyList<ChannelCard> Channels, Page<VideoCard> Videos);

public sealed record Suggestion(string Text, SuggestionKind Kind, string Id);

/// <summary>Channels first, then video titles.</summary>
public sealed record SuggestResponse(IReadOnlyList<Suggestion> Suggestions);
