using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TonTuyau.Api.Contracts;
using TonTuyau.Api.Errors;
using TonTuyau.Application.Accounts;
using TonTuyau.Application.Browse;
using TonTuyau.Application.Validation;

namespace TonTuyau.Api.Controllers;

/// <summary>
/// The admin's settings of who logs in and who watches (2.3.0): the library for everyone, or for the logged-in accounts
/// only. An admin's session only (the default policy).
/// </summary>
[ApiController]
[Authorize]
[Route("api/admin/settings")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, Problems.ContentType)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, Problems.ContentType)]
public sealed class SettingsController(AccessService access) : ControllerBase
{
    /// <summary>The largest body taken: a flag fits in far less.</summary>
    public const int MaxRequestBytes = 1024;

    /// <summary>Who can watch.</summary>
    [HttpGet("access")]
    [ProducesResponseType<AccessSettingsResponse>(StatusCodes.Status200OK)]
    public async Task<AccessSettingsResponse> AccessAsync(CancellationToken cancellationToken) =>
        (AccessSettingsResponse)await access
            .GetAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <summary>Opens the library to everyone, or closes it to the logged-in accounts only, at once.</summary>
    [HttpPut("access")]
    [Consumes("application/json")]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType<AccessSettingsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    public async Task<AccessSettingsResponse> SaveAccessAsync(
        AccessSettingsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.AccountsOnly is null)
        {
            throw new ValidationException(
                RequestRules.Subject,
                [new ValidationError("accountsOnly", "is required: true or false.")]);
        }

        string by = User.Identity?.Name ?? throw new InvalidOperationException("A logged-in request without a name.");
        return (AccessSettingsResponse)await access
            .SaveAsync(request.AccountsOnly.Value, by, cancellationToken)
            .ConfigureAwait(false);
    }
}
