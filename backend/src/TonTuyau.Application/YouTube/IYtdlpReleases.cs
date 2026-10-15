namespace TonTuyau.Application.YouTube;

/// <summary>yt-dlp's releases on GitHub (1.5): only those with a Linux program, the latest first.</summary>
public interface IYtdlpReleases
{
    /// <exception cref="YtdlpInstallException">GitHub can't be reached or answers something unexpected.</exception>
    Task<IReadOnlyList<YtdlpRelease>> ListAsync(YtdlpChannel channel, CancellationToken cancellationToken);
}
