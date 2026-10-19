namespace TonTuyau.Infrastructure.Elasticsearch.Documents;

/// <summary>What <c>_mget</c> reads of a document before it's written again: its content's hash.</summary>
internal sealed record StoreBookkeeping(string? ContentHash);

/// <summary>Partial update: a video thumbnail's recorded versions.</summary>
internal sealed record ThumbnailArtPartial(ArtDocument ThumbnailArt);

/// <summary>Partial update: a channel picture's recorded versions.</summary>
internal sealed record ThumbArtPartial(ArtDocument ThumbArt);

/// <summary>Partial update: a channel banner's recorded versions.</summary>
internal sealed record BannerArtPartial(ArtDocument BannerArt);

/// <summary>Partial update: "file missing on the share" flag.</summary>
internal sealed record MediaMissingPartial(bool MediaMissing);

/// <summary>A video as lists read it (source-filtered: no description, no tracks).</summary>
internal sealed record VideoListDocument(
    string Id,
    string ChannelId,
    string ChannelName,
    string Title,
    string Type,
    string? YoutubeState,
    DateTimeOffset Published,
    double DurationSeconds,
    long Views,
    string ThumbnailPath,
    ArtDocument? ThumbnailArt = null,
    DateTimeOffset? YoutubeCheckedAt = null,
    string? YoutubeReason = null);

/// <summary>A channel as lists read it.</summary>
internal sealed record ChannelListDocument(
    string Id,
    string Name,
    long Subscribers,
    string? ThumbPath,
    ArtDocument? ThumbArt = null);

/// <summary>A video title, for suggestions.</summary>
internal sealed record TitleDocument(string Id, string Title);

/// <summary>A channel name, for suggestions.</summary>
internal sealed record NameDocument(string Id, string Name);
