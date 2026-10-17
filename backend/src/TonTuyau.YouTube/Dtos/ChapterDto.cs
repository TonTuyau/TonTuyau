using System.Text.Json.Serialization;

namespace TonTuyau.YouTube.Dtos;

/// <summary>An entry of yt-dlp's <c>chapters</c>.</summary>
internal sealed record ChapterDto(
    [property: JsonPropertyName("start_time")] double? StartTime,
    [property: JsonPropertyName("end_time")] double? EndTime,
    [property: JsonPropertyName("title")] string? Title);
