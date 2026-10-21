using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TonTuyau.Api.Contracts;
using TonTuyau.Api.Errors;
using TonTuyau.Api.Hosting;
using TonTuyau.Application.Extension;
using TonTuyau.Application.YouTube;

namespace TonTuyau.Api.Controllers;

/// <summary>
/// What TonTuyau's browser extension, LaPompe, calls (1.5), with a <c>browserExtension</c> API key (or an admin's
/// session). Its addresses stay under <c>/api/extension</c>: the extension's versions out there call them.
/// </summary>
[ApiController]
[Authorize(Policy = AuthServices.BrowserExtensionPolicy)]
[Route("api/extension")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, Problems.ContentType)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, Problems.ContentType)]
public sealed class PumpController(YouTubeCookieService cookies, PumpService adding) : ControllerBase
{
    /// <summary>The largest cookie file taken (with room for the JSON around it).</summary>
    public const int MaxCookieRequestBytes = NetscapeCookies.MaxLength + 4096;

    /// <summary>
    /// The connection test: TonTuyau's version, the key (or account) the request came with, and whether it takes "Add to
    /// TonTuyau" (2.0).
    /// </summary>
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

    /// <summary>Where a video stands in TonTuyau (contract v2.6). 400 when it isn't a video id.</summary>
    [HttpGet("videos/{id}")]
    [ProducesResponseType<ExtensionItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    public async Task<ExtensionItemResponse> GetVideoAsync(string id, CancellationToken cancellationToken) =>
        (ExtensionItemResponse)await adding
            .GetVideoAsync(id, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// "Add to TonTuyau" on a video (contract v2.6): downloaded through the run queue, as "Download a video" on the Tasks
    /// page; one already waiting gives its run. 409 when TonTuyau has it, with its file; 400 when it isn't a video id.
    /// </summary>
    [HttpPost("videos/{id}")]
    [ProducesResponseType<ExtensionItemResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, Problems.ContentType)]
    public async Task<AcceptedResult> AddVideoAsync(string id, CancellationToken cancellationToken)
    {
        ExtensionItemResponse item = (ExtensionItemResponse)await adding
            .AddVideoAsync(id, Name(), cancellationToken)
            .ConfigureAwait(false);
        return Accepted(item);
    }

    /// <summary>Where a channel stands in TonTuyau (contract v2.6). 400 when it isn't a channel id.</summary>
    [HttpGet("channels/{id}")]
    [ProducesResponseType<ExtensionItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    public async Task<ExtensionItemResponse> GetChannelAsync(string id, CancellationToken cancellationToken) =>
        (ExtensionItemResponse)await adding
            .GetChannelAsync(id, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// "Add to TonTuyau" on a channel (contract v2.6): subscribed to with the YouTube page's default tabs, added when
    /// it's new, through the run queue, as "Add a channel"; one already waiting gives its run. 409 when TonTuyau is
    /// subscribed to it; 400 when it isn't a channel id.
    /// </summary>
    [HttpPost("channels/{id}")]
    [ProducesResponseType<ExtensionItemResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, Problems.ContentType)]
    public async Task<AcceptedResult> AddChannelAsync(string id, CancellationToken cancellationToken)
    {
        ExtensionItemResponse item = (ExtensionItemResponse)await adding
            .AddChannelAsync(id, Name(), cancellationToken)
            .ConfigureAwait(false);
        return Accepted(item);
    }

    private string Name() =>
        User.Identity?.Name ?? throw new InvalidOperationException("A request without a name.");
}
