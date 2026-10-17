namespace TonTuyau.Api.Contracts;

/// <summary>A yt-dlp version that can be chosen.</summary>
public sealed record YtdlpVersionResponse(string Version, DateTime PublishedAt);
