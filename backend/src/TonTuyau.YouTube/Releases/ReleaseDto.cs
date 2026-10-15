using System.Text.Json.Serialization;

namespace TonTuyau.YouTube.Releases;

/// <summary>A GitHub release, as <c>GET /repos/{owner}/{repo}/releases</c> lists it (the fields read here).</summary>
internal sealed record ReleaseDto(
    [property: JsonPropertyName("tag_name")] string? TagName,
    [property: JsonPropertyName("published_at")] DateTimeOffset? PublishedAt,
    [property: JsonPropertyName("draft")] bool Draft,
    [property: JsonPropertyName("assets")] IReadOnlyList<AssetDto>? Assets);
