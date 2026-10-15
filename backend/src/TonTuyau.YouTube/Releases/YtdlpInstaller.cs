using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using TonTuyau.Application.Configuration;
using TonTuyau.Application.YouTube;

namespace TonTuyau.YouTube.Releases;

/// <summary>
/// The yt-dlp versions in TonTuyau's tools folder: <c>yt-dlp-&lt;version&gt;</c>, and <c>active</c> naming the one in use
/// (empty: the image's copy). A version is installed only once its program matches the SHA-256 its release publishes
/// and, run with <c>--version</c>, says it's that version.
/// </summary>
internal sealed class YtdlpInstaller(HttpClient http, TonTuyauOptions options) : IYtdlpInstaller
{
    /// <summary>How long trying a program may take (the standalone program unpacks itself the first time).</summary>
    public static readonly TimeSpan TryTimeout = TimeSpan.FromSeconds(60);

    private const string ActiveFile = "active";

    private string Root => options.YouTube.ToolsRoot;

    public string PathOf(string version) =>
        YtdlpRepositories.IsVersion(version)
            ? Path.Combine(Root, $"yt-dlp-{version}")
            : throw new ArgumentException($"Not a yt-dlp version: {version}", nameof(version));

    public bool IsInstalled(string version) => YtdlpRepositories.IsVersion(version) && File.Exists(PathOf(version));

    public async Task InstallAsync(YtdlpRelease release, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(release);
        string target = PathOf(release.Version);
        Directory.CreateDirectory(Root);
        string expected = await ChecksumAsync(release, cancellationToken).ConfigureAwait(false);
        string part = target + ".part";
        try
        {
            string actual = await DownloadAsync(release, part, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new YtdlpInstallException(
                    $"yt-dlp {release.Version}'s program doesn't match the checksum its release publishes.");
            }

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(
                    part,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                    | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                    | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }

            string? said = await RunVersionAsync(part, cancellationToken).ConfigureAwait(false);
            if (said != release.Version)
            {
                throw new YtdlpInstallException(
                    $"yt-dlp {release.Version}'s program doesn't run here (it said: {said ?? "nothing"}).");
            }

            File.Move(part, target, overwrite: true);
        }
        finally
        {
            File.Delete(part);
        }
    }

    public string? ReadActive()
    {
        string file = Path.Combine(Root, ActiveFile);
        if (!File.Exists(file))
        {
            return null;
        }

        string version = File
            .ReadAllText(file)
            .Trim();
        return IsInstalled(version) ? version : null;
    }

    public void SetActive(string version)
    {
        Directory.CreateDirectory(Root);
        File.WriteAllText(Path.Combine(Root, ActiveFile), version);
        foreach (string program in Directory.EnumerateFiles(Root, "yt-dlp-*"))
        {
            if (version.Length == 0 || program != PathOf(version))
            {
                File.Delete(program);
            }
        }
    }

    public Task<string?> VersionOfAsync(string program, CancellationToken cancellationToken) =>
        RunVersionAsync(program, cancellationToken);

    /// <summary>What a program says with <c>--version</c>; null when it can't be run or says nothing in time.</summary>
    private static async Task<string?> RunVersionAsync(string program, CancellationToken cancellationToken)
    {
        ProcessStartInfo start = new(program, "--version")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TryTimeout);
        try
        {
            using Process? process = Process.Start(start);
            if (process is null)
            {
                return null;
            }

            string output = await process.StandardOutput
                .ReadToEndAsync(timeout.Token)
                .ConfigureAwait(false);
            await process
                .WaitForExitAsync(timeout.Token)
                .ConfigureAwait(false);
            return process.ExitCode == 0 ? output.Trim() : null;
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException or UnauthorizedAccessException
            || ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    /// <summary>The program's SHA-256, from the release's <c>SHA2-256SUMS</c> ("&lt;hash&gt;  yt-dlp_linux").</summary>
    private async Task<string> ChecksumAsync(YtdlpRelease release, CancellationToken cancellationToken)
    {
        Uri url = YtdlpRepositories.FileUrl(release, YtdlpRepositories.Checksums);
        string sums = await GetAsync(url, cancellationToken).ConfigureAwait(false);
        foreach (string line in sums.Split('\n'))
        {
            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && parts[1] == YtdlpRepositories.LinuxProgram && parts[0].Length == 64)
            {
                return parts[0];
            }
        }

        throw new YtdlpInstallException($"yt-dlp {release.Version}'s checksums don't name its Linux program.");
    }

    /// <returns>The file's SHA-256, hex.</returns>
    private async Task<string> DownloadAsync(YtdlpRelease release, string file, CancellationToken cancellationToken)
    {
        Uri url = YtdlpRepositories.FileUrl(release, YtdlpRepositories.LinuxProgram);
        try
        {
            using HttpResponseMessage response = await http
                .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new YtdlpInstallException(
                    $"GitHub answered {(int)response.StatusCode} to yt-dlp {release.Version}'s program.");
            }

            Stream body = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            await using (body.ConfigureAwait(false))
            {
                FileStream output = new(file, FileMode.Create, FileAccess.Write);
                await using (output.ConfigureAwait(false))
                {
                    using IncrementalHash sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    byte[] buffer = new byte[81920];
                    int read;
                    while ((read = await body.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        sha.AppendData(buffer, 0, read);
                        await output
                            .WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                            .ConfigureAwait(false);
                    }

                    return Convert.ToHexStringLower(sha.GetHashAndReset());
                }
            }
        }
        catch (HttpRequestException ex)
        {
            throw new YtdlpInstallException($"yt-dlp {release.Version}'s program can't be downloaded from GitHub.", ex);
        }
    }

    private async Task<string> GetAsync(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await http
                .GetAsync(url, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new YtdlpInstallException($"GitHub answered {(int)response.StatusCode} to {url}.");
            }

            return await response.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new YtdlpInstallException($"GitHub can't be reached ({url}).", ex);
        }
    }
}
