using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TonTuyau.Api.Contracts;
using TonTuyau.Api.Errors;
using TonTuyau.Api.Hosting;

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
public sealed class ExtensionController : ControllerBase
{
    /// <summary>The connection test: TonTuyau's version, and the key (or account) the request came with.</summary>
    [HttpGet("connection")]
    [ProducesResponseType<ConnectionResponse>(StatusCodes.Status200OK)]
    public ConnectionResponse Connection() =>
        new(AppVersion.Value, User.Identity?.Name ?? throw new InvalidOperationException("A request without a name."));
}
