using System.Text.Json.Serialization;

namespace TonTuyau.YouTube.Dtos;

/// <summary>An entry of yt-dlp's <c>comments</c> (with <c>--write-comments</c>).</summary>
/// <param name="Parent"><c>root</c> for a comment on the video, else the id of the comment it answers.</param>
internal sealed record CommentDto(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("parent")] string? Parent,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("author")] string? Author,
    [property: JsonPropertyName("author_id")] string? AuthorId,
    [property: JsonPropertyName("author_thumbnail")] string? AuthorThumbnail,
    [property: JsonPropertyName("timestamp")] long? Timestamp,
    [property: JsonPropertyName("like_count")] long? LikeCount,
    [property: JsonPropertyName("is_pinned")] bool? IsPinned,
    [property: JsonPropertyName("author_is_uploader")] bool? AuthorIsUploader,
    [property: JsonPropertyName("is_favorited")] bool? IsFavorited);
