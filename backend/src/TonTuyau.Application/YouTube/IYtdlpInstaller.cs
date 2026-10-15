namespace TonTuyau.Application.YouTube;

/// <summary>
/// The yt-dlp programs in TonTuyau's tools folder (1.5): installing a version (downloaded from its GitHub release,
/// checked against the checksums published with it, tried), and which one is active.
/// </summary>
public interface IYtdlpInstaller
{
    /// <summary>Where that version is (or would be) installed.</summary>
    string PathOf(string version);

    bool IsInstalled(string version);

    /// <summary>Downloads, checks and tries that version, then installs it.</summary>
    /// <exception cref="YtdlpInstallException">The download, the checksum or the try failed: nothing is installed.</exception>
    Task InstallAsync(YtdlpRelease release, CancellationToken cancellationToken);

    /// <summary>The version made active last (kept in the tools folder), if it's still there.</summary>
    string? ReadActive();

    /// <summary>Notes the active version (empty: the image's copy), and removes the other versions installed.</summary>
    void SetActive(string version);

    /// <summary>What a yt-dlp program says it is (<c>--version</c>); null when it can't be run.</summary>
    Task<string?> VersionOfAsync(string program, CancellationToken cancellationToken);
}
