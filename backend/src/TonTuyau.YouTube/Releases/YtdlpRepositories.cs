using System.Text.RegularExpressions;
using TonTuyau.Application.YouTube;

namespace TonTuyau.YouTube.Releases;

/// <summary>
/// The only places yt-dlp is taken from: its two GitHub repositories, their Linux program and checksums. Versions are
/// checked against a strict pattern before any path or URL is built with them.
/// </summary>
internal static partial class YtdlpRepositories
{
    public const string LinuxProgram = "yt-dlp_linux";
    public const string Checksums = "SHA2-256SUMS";

    /// <summary>GitHub's API, where the releases are listed.</summary>
    public const string ApiUrl = "https://api.github.com";

    /// <summary>GitHub, where the release files are downloaded from.</summary>
    public const string DownloadsUrl = "https://github.com";

    public static string Repository(YtdlpChannel channel) => channel switch
    {
        YtdlpChannel.Nightly => "yt-dlp/yt-dlp-nightly-builds",
        _ => "yt-dlp/yt-dlp",
    };

    /// <summary><c>2026.08.19</c>, or <c>2026.10.10.233141</c> for a nightly build.</summary>
    public static bool IsVersion(string? version) => version is not null && VersionPattern().IsMatch(version);

    /// <summary>A file of that release, on GitHub: built here, never followed from an answer.</summary>
    public static Uri FileUrl(YtdlpRelease release, string file) =>
        new($"{DownloadsUrl}/{Repository(release.Channel)}/releases/download/{release.Version}/{file}");

    [GeneratedRegex(@"^\d{4}\.\d{2}\.\d{2}(\.\d{1,6})?$")]
    private static partial Regex VersionPattern();
}
