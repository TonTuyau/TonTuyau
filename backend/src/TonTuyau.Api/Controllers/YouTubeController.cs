using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TonTuyau.Api.Contracts;
using TonTuyau.Api.Errors;
using TonTuyau.Application.YouTube;

namespace TonTuyau.Api.Controllers;

/// <summary>
/// The admin's YouTube page (1.5): the cookie TonTuyau signs in with (checking it again, removing it) and the tools'
/// versions. An admin's session only.
/// </summary>
[ApiController]
[Authorize]
[Route("api/admin/youtube")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, Problems.ContentType)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, Problems.ContentType)]
public sealed class YouTubeController(YouTubeCookieService cookies) : ControllerBase
{
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

    /// <summary>The logged-in account's name ([Authorize]: there is one).</summary>
    private string Account() =>
        User.Identity?.Name ?? throw new InvalidOperationException("A logged-in request without a name.");
}
