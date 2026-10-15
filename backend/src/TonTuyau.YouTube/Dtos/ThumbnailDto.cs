using System.Text.Json.Serialization;

namespace TonTuyau.YouTube.Dtos;

/// <summary>An entry of yt-dlp's <c>thumbnails</c>.</summary>
internal sealed record ThumbnailDto(
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("width")] int? Width,
    [property: JsonPropertyName("height")] int? Height);
