using System.Net.Http.Json;
using TonTuyau.Application.YouTube;

namespace TonTuyau.YouTube.Releases;

/// <summary>
/// yt-dlp's releases, listed by GitHub's API (no token: 60 requests an hour per address, so the lists are kept an hour).
/// Only releases with a Linux program and checksums count; the latest first.
/// </summary>
internal sealed class GitHubYtdlpReleases(HttpClient http, TimeProvider clock)
    : IYtdlpReleases
{
    public static readonly TimeSpan KeptFor = TimeSpan.FromHours(1);

    /// <summary>GitHub's largest page; the official releases take two.</summary>
    private const int PageSize = 100;

    private const int MaxPages = 5;

    private readonly Lock _gate = new();
    private readonly Dictionary<YtdlpChannel, (DateTimeOffset At, IReadOnlyList<YtdlpRelease> Releases)> _kept = [];

    public async Task<IReadOnlyList<YtdlpRelease>> ListAsync(YtdlpChannel channel, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_kept.TryGetValue(channel, out (DateTimeOffset At, IReadOnlyList<YtdlpRelease> Releases) kept)
                && clock.GetUtcNow() - kept.At < KeptFor)
            {
                return kept.Releases;
            }
        }

        List<YtdlpRelease> releases = [];
        // The nightly builds of two months fit a page; the official releases are all wanted.
        int pages = channel == YtdlpChannel.Nightly ? 1 : MaxPages;
        for (int page = 1; page <= pages; page++)
        {
            IReadOnlyList<ReleaseDto> listed = await PageAsync(channel, page, cancellationToken).ConfigureAwait(false);
            releases.AddRange(listed
                .Where(IsUsable)
                .Select(r => new YtdlpRelease(r.TagName!, channel, r.PublishedAt!.Value)));
            if (listed.Count < PageSize)
            {
                break;
            }
        }

        List<YtdlpRelease> latestFirst = [.. releases.OrderByDescending(r => r.PublishedAt)];
        lock (_gate)
        {
            _kept[channel] = (clock.GetUtcNow(), latestFirst);
        }

        return latestFirst;
    }

    private static bool IsUsable(ReleaseDto release) =>
        !release.Draft
        && YtdlpRepositories.IsVersion(release.TagName)
        && release.PublishedAt is not null
        && release.Assets is { } assets
        && assets.Any(a => a.Name == YtdlpRepositories.LinuxProgram)
        && assets.Any(a => a.Name == YtdlpRepositories.Checksums);

    private async Task<IReadOnlyList<ReleaseDto>> PageAsync(
        YtdlpChannel channel,
        int page,
        CancellationToken cancellationToken)
    {
        Uri url = new(
            $"{YtdlpRepositories.ApiUrl}/repos/{YtdlpRepositories.Repository(channel)}/releases"
            + $"?per_page={PageSize}&page={page}");
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, url);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using HttpResponseMessage response = await http
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new YtdlpInstallException(
                    $"GitHub answered {(int)response.StatusCode} to the list of yt-dlp's releases.");
            }

            return await response.Content
                .ReadFromJsonAsync<List<ReleaseDto>>(cancellationToken)
                .ConfigureAwait(false) ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException
            or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new YtdlpInstallException("GitHub can't be reached to list yt-dlp's releases.", ex);
        }
    }
}
