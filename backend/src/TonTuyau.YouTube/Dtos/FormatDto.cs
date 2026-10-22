using System.Text.Json.Serialization;

namespace TonTuyau.YouTube.Dtos;

/// <summary>One format of a download, as yt-dlp's info dict describes it (2.0): its codecs, size and bitrate.</summary>
/// <param name="Vcodec">The video codec as YouTube names it (<c>vp09.00.40.08</c>), or <c>none</c>.</param>
/// <param name="Tbr">Its total bitrate, in kbit/s.</param>
internal sealed record FormatDto(
    [property: JsonPropertyName("vcodec")] string? Vcodec,
    [property: JsonPropertyName("acodec")] string? Acodec,
    [property: JsonPropertyName("width")] int? Width,
    [property: JsonPropertyName("height")] int? Height,
    [property: JsonPropertyName("tbr")] double? Tbr);
