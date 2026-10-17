using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TonTuyau.Api.Contracts;
using TonTuyau.Api.Errors;
using TonTuyau.Api.Hosting;
using TonTuyau.Application.YouTube;

namespace TonTuyau.Api.Controllers;

/// <summary>
/// What TonTuyau's browser extension calls (1.5), with a <c>browserExtension</c> API key (or an admin's session).
/// </summary>
[ApiController]
[Authorize(Policy = AuthServices.BrowserExtensionPolicy)]
[Route("api/extension")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, Problems.ContentType)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, Problems.ContentType)]
public sealed class ExtensionController(YouTubeCookieService cookies) : ControllerBase
{
    /// <summary>The largest cookie file taken (with room for the JSON around it).</summary>
    public const int MaxCookieRequestBytes = NetscapeCookies.MaxLength + 4096;

    /// <summary>The connection test: TonTuyau's version, and the key (or account) the request came with.</summary>
    [HttpGet("connection")]
    [ProducesResponseType<ConnectionResponse>(StatusCodes.Status200OK)]
    public ConnectionResponse Connection() =>
        new(AppVersion.Value, Name());

    /// <summary>The YouTube cookie TonTuyau has: when, from whom, whether it works; never its content.</summary>
    [HttpGet("youtube-cookie")]
    [ProducesResponseType<ExtensionCookieResponse>(StatusCodes.Status200OK)]
    public async Task<ExtensionCookieResponse> GetCookieAsync(CancellationToken cancellationToken)
    {
        YouTubeCookieStatus? status = await cookies
            .GetStatusAsync(cancellationToken)
            .ConfigureAwait(false);
        return new ExtensionCookieResponse(status is null ? null : (YouTubeCookieResponse)status);
    }

    /// <summary>
    /// Sends the browser's YouTube cookie: it replaces the one TonTuyau had, and is checked against YouTube at once (a
    /// few seconds). 400 when it holds no <c>youtube.com</c> cookie.
    /// </summary>
    [HttpPut("youtube-cookie")]
    [Consumes("application/json")]
    [RequestSizeLimit(MaxCookieRequestBytes)]
    [ProducesResponseType<YouTubeCookieResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    public async Task<YouTubeCookieResponse> SendCookieAsync(
        SendCookieRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return (YouTubeCookieResponse)await cookies
            .SetAsync(request.Cookies, Name(), cancellationToken)
            .ConfigureAwait(false);
    }

    private string Name() =>
        User.Identity?.Name ?? throw new InvalidOperationException("A request without a name.");
}
