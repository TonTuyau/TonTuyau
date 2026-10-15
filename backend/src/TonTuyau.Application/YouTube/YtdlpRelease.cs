namespace TonTuyau.Application.YouTube;

/// <summary>A yt-dlp version TonTuyau can run: a release with a Linux program and its checksums (1.5).</summary>
/// <param name="Version">Its tag, <c>2026.08.19</c> or <c>2026.10.10.233141</c> (nightly): what <c>--version</c> prints.</param>
public sealed record YtdlpRelease(string Version, YtdlpChannel Channel, DateTimeOffset PublishedAt);
