using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TonTuyau.Infrastructure.Elasticsearch.Documents;

// What TonTuyau stores in Elasticsearch. Field names (snake_case) must match Definitions/*.json, which is
// "dynamic": "strict": a field missing there makes every write fail (a unit test checks they match).

internal interface IStoreDocument
{
    string Id { get; }

    string? ContentHash { get; }
}

internal sealed record VideoDocument(
    string Id,
    string ChannelId,
    string ChannelName,
    string Title,
    string Description,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Categories,
    string Type,
    DateTimeOffset Published,
    DateTimeOffset Downloaded,
    double DurationSeconds,
    long MediaSize,
    string MediaPath,
    bool MediaPathCorrected,
    string ThumbnailPath,
    long Views,
    long Likes,
    long Dislikes,
    IReadOnlyList<TrackDocument> Tracks,
    string YoutubeState,
    DateTimeOffset? YoutubeCheckedAt,
    string? YoutubeReason,
    DateTimeOffset? YoutubeRefreshedAt,
    string? ContentHash = null,
    bool? MediaMissing = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ArtDocument? ThumbnailArt = null)
    : IStoreDocument;

/// <summary>
/// Bookkeeping: what the art store holds for one image (resized versions, QA09), recorded by the import or a refresh.
/// Left out of the JSON when unset, so the content hash of a document without it is what it always was, and a write
/// to an index made before resized art (strict mapping without the field) still works.
/// </summary>
internal sealed record ArtDocument(int Width, IReadOnlyList<int> Sizes);

internal sealed record TrackDocument(string Kind, string Codec, long Bitrate, int? Width, int? Height);

internal sealed record ChannelDocument(
    string Id,
    string Name,
    string Description,
    long Subscribers,
    IReadOnlyList<string> Tags,
    string? ThumbPath,
    string? BannerPath,
    string? TvartPath,
    string YoutubeState,
    DateTimeOffset? YoutubeCheckedAt,
    string? YoutubeReason,
    DateTimeOffset? YoutubeRefreshedAt,
    string? ContentHash = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ArtDocument? ThumbArt = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ArtDocument? BannerArt = null)
    : IStoreDocument;

internal static class StoreJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>
    /// Hash of the content only: <c>media_missing</c>, <c>content_hash</c> and the art records are bookkeeping.
    /// </summary>
    public static string ContentHash<T>(T contentOnly) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(contentOnly, Options)))[..32];
}
