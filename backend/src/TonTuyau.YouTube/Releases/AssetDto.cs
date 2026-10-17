using System.Text.Json.Serialization;

namespace TonTuyau.YouTube.Releases;

/// <summary>A file of a GitHub release (its name: the URL is built from the release's tag, never taken from here).</summary>
internal sealed record AssetDto([property: JsonPropertyName("name")] string? Name);
