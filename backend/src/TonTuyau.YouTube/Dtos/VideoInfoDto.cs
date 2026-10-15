using System.Text.Json.Serialization;

namespace TonTuyau.YouTube.Dtos;

/// <summary>
/// The fields TonTuyau reads from yt-dlp's <c>--dump-single-json</c> for a video (its "info dict"); yt-dlp gives many
/// more.
/// </summary>
/// <param name="Timestamp">Publication, as Unix seconds; often absent, then <paramref name="UploadDate"/> (YYYYMMDD).</param>
internal sealed record VideoInfoDto(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("channel_id")] string? ChannelId,
    [property: JsonPropertyName("channel")] string? Channel,
    [property: JsonPropertyName("timestamp")] long? Timestamp,
    [property: JsonPropertyName("upload_date")] string? UploadDate,
    [property: JsonPropertyName("duration")] double? Duration,
    [property: JsonPropertyName("view_count")] long? ViewCount,
    [property: JsonPropertyName("like_count")] long? LikeCount,
    [property: JsonPropertyName("tags")] IReadOnlyList<string>? Tags,
    [property: JsonPropertyName("categories")] IReadOnlyList<string>? Categories,
    [property: JsonPropertyName("thumbnails")] IReadOnlyList<ThumbnailDto>? Thumbnails,
    [property: JsonPropertyName("live_status")] string? LiveStatus,
    [property: JsonPropertyName("comment_count")] long? CommentCount,
    [property: JsonPropertyName("comments")] IReadOnlyList<CommentDto>? Comments,
    [property: JsonPropertyName("chapters")] IReadOnlyList<ChapterDto>? Chapters);
