using System.Text.Json.Serialization;

namespace TonTuyau.YouTube.Dtos;

/// <summary>
/// The fields TonTuyau reads from yt-dlp's <c>--dump-single-json --flat-playlist --playlist-items 0</c> on a
/// channel's page (the channel itself, without its videos).
/// </summary>
/// <param name="UploaderId">The channel's <c>@handle</c>.</param>
internal sealed record ChannelInfoDto(
    [property: JsonPropertyName("channel_id")] string? ChannelId,
    [property: JsonPropertyName("channel")] string? Channel,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("uploader_id")] string? UploaderId,
    [property: JsonPropertyName("channel_follower_count")] long? ChannelFollowerCount,
    [property: JsonPropertyName("tags")] IReadOnlyList<string>? Tags,
    [property: JsonPropertyName("thumbnails")] IReadOnlyList<ThumbnailDto>? Thumbnails);
