namespace TonTuyau.Application.YouTube;

/// <summary>The programs TonTuyau reads YouTube with, and their versions; null when one can't be run.</summary>
public sealed record YouTubeTools(string? YtdlpVersion, string? DenoVersion);
