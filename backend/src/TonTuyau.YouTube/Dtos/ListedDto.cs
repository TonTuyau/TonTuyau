using System.Text.Json.Serialization;

namespace TonTuyau.YouTube.Dtos;

/// <summary>One line of a tab's listing (2.0, B5): what <c>--print "%(.{id,live_status})j"</c> writes per entry.</summary>
internal sealed record ListedDto(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("live_status")] string? LiveStatus);
