namespace TonTuyau.Infrastructure.Elasticsearch.Documents;

/// <summary>The bookkeeping fields of a stored document (what <c>_mget</c> reads before an upsert).</summary>
internal sealed record StoreBookkeeping(
    string? ContentHash,
    string? SeenInRun,
    bool? MediaMissing,
    ArtDocument? ThumbnailArt = null,
    ArtDocument? ThumbArt = null,
    ArtDocument? BannerArt = null);

/// <summary>Partial update: a video thumbnail's recorded versions.</summary>
internal sealed record ThumbnailArtPartial(ArtDocument ThumbnailArt);

/// <summary>Partial update: a channel picture's recorded versions.</summary>
internal sealed record ThumbArtPartial(ArtDocument ThumbArt);

/// <summary>Partial update: a channel banner's recorded versions.</summary>
internal sealed record BannerArtPartial(ArtDocument BannerArt);

/// <summary>Partial update: "seen by this run".</summary>
internal sealed record SeenInRunPartial(string SeenInRun);

/// <summary>Partial update: "file missing on the share" flag.</summary>
internal sealed record MediaMissingPartial(bool MediaMissing);

/// <summary>A video as the sync's deletion planning reads it (a source-filtered search hit).</summary>
internal sealed record VideoSummaryDocument(
    string Id,
    string Title,
    string ChannelId,
    string Type,
    DateTimeOffset Downloaded,
    string? SeenInRun,
    string ThumbnailPath);

/// <summary>A channel as the sync's deletion planning reads it.</summary>
internal sealed record ChannelSummaryDocument(
    string Id,
    string Name,
    string? SeenInRun,
    string? ThumbPath,
    string? BannerPath,
    string? TvartPath);

/// <summary>Just the id (e.g. "which videos are flagged").</summary>
internal sealed record IdDocument(string Id);

/// <summary>A video as lists read it (source-filtered: no description, no tracks).</summary>
internal sealed record VideoListDocument(
    string Id,
    string ChannelId,
    string ChannelName,
    string Title,
    string Type,
    bool Active,
    DateTimeOffset Published,
    double DurationSeconds,
    long Views,
    string ThumbnailPath,
    ArtDocument? ThumbnailArt = null);

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
