using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TonTuyau.Api.Contracts;
using TonTuyau.Api.Errors;
using TonTuyau.Application.YouTube;

namespace TonTuyau.Api.Controllers;

/// <summary>
/// The admin's YouTube page (1.5): the cookie TonTuyau signs in with (checking it again, removing it), the tools'
/// versions, which yt-dlp runs (the latest official release by default, or a version chosen), and how YouTube is read
/// (the comments or not). An admin's session only.
/// </summary>
[ApiController]
[Authorize]
[Route("api/admin/youtube")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, Problems.ContentType)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, Problems.ContentType)]
public sealed class YouTubeController(
    YouTubeCookieService cookies,
    YtdlpService ytdlp,
    YouTubeSettingsService settings) : ControllerBase
{
    /// <summary>The largest choice taken: a kind and a version fit in far less.</summary>
    public const int MaxRequestBytes = 1024;

    /// <summary>The cookie (null when there's none) and the versions of yt-dlp and Deno (null when one can't be run).</summary>
    [HttpGet]
    [ProducesResponseType<YouTubeResponse>(StatusCodes.Status200OK)]
    public async Task<YouTubeResponse> GetAsync(CancellationToken cancellationToken) =>
        (YouTubeResponse)await cookies
            .GetOverviewAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <summary>Tries the cookie against YouTube again (a few seconds). 404 when there's none.</summary>
    [HttpPost("cookie/check")]
    [ProducesResponseType<YouTubeCookieResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, Problems.ContentType)]
    public async Task<YouTubeCookieResponse> CheckAsync(CancellationToken cancellationToken) =>
        (YouTubeCookieResponse)await cookies
            .CheckAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <summary>Removes the cookie: YouTube is read signed out until the extension sends another. 404 when there's none.</summary>
    [HttpDelete("cookie")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, Problems.ContentType)]
    public async Task<IActionResult> DeleteAsync(CancellationToken cancellationToken)
    {
        await cookies
            .DeleteAsync(Account(), cancellationToken)
            .ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>Which yt-dlp is chosen, which runs, whether another waits for the task going on, the last check.</summary>
    [HttpGet("ytdlp")]
    [ProducesResponseType<YtdlpStateResponse>(StatusCodes.Status200OK)]
    public async Task<YtdlpStateResponse> GetYtdlpAsync(CancellationToken cancellationToken) =>
        (YtdlpStateResponse)await ytdlp
            .GetStateAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <summary>The versions that can be chosen (from GitHub, kept an hour). 502 when GitHub can't be reached.</summary>
    [HttpGet("ytdlp/versions")]
    [ProducesResponseType<YtdlpVersionsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway, Problems.ContentType)]
    public async Task<YtdlpVersionsResponse> GetYtdlpVersionsAsync(CancellationToken cancellationToken) =>
        (YtdlpVersionsResponse)await ytdlp
            .ListVersionsAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Chooses which yt-dlp runs: 202, it's downloaded, checked and tried in the background, then used from the next
    /// task (GET ytdlp says where it's at). 400 when the version isn't one offered.
    /// </summary>
    [HttpPut("ytdlp")]
    [Consumes("application/json")]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType<YtdlpStateResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway, Problems.ContentType)]
    public async Task<AcceptedResult> ChooseYtdlpAsync(ChooseYtdlpRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        YtdlpState state = await ytdlp
            .ChooseAsync(request.Kind, request.Version, Account(), cancellationToken)
            .ConfigureAwait(false);
        return Accepted(new Uri("/api/admin/youtube/ytdlp", UriKind.Relative), (YtdlpStateResponse)state);
    }

    /// <summary>How YouTube is read: which comments the refreshes read (none by default, the top N, or all).</summary>
    [HttpGet("settings")]
    [ProducesResponseType<YouTubeSettingsResponse>(StatusCodes.Status200OK)]
    public async Task<YouTubeSettingsResponse> GetSettingsAsync(CancellationToken cancellationToken) =>
        (YouTubeSettingsResponse)await settings
            .GetAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <summary>Changes how YouTube is read, from the next refresh on. 400 when a setting is missing or wrong.</summary>
    [HttpPut("settings")]
    [Consumes("application/json")]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType<YouTubeSettingsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    public async Task<YouTubeSettingsResponse> SaveSettingsAsync(
        YouTubeSettingsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return (YouTubeSettingsResponse)await settings
            .SaveAsync(
                request.Comments,
                request.CommentsMax,
                request.PauseSeconds,
                request.HoldMinutes,
                request.SyncEveryMinutes,
                request.DownloadFormat,
                request.DefaultTabs?.ToTabs("defaultTabs"),
                request.MinFreeGb,
                request.HoldBelowMinFree,
                Account(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>The logged-in account's name ([Authorize]: there is one).</summary>
    private string Account() =>
        User.Identity?.Name ?? throw new InvalidOperationException("A logged-in request without a name.");
}
