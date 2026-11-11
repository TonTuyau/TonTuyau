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
/// only; the LDAP directory, and its tests. An admin's session only (the default policy).
/// </summary>
[ApiController]
[Authorize]
[Route("api/admin/settings")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, Problems.ContentType)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, Problems.ContentType)]
public sealed class SettingsController(AccessService access, LdapService ldap) : ControllerBase
{
    /// <summary>The largest body taken: a flag fits in far less.</summary>
    public const int MaxRequestBytes = 1024;

    /// <summary>The largest LDAP settings' body taken: a CA certificate fits in far less.</summary>
    public const int MaxLdapRequestBytes = 65_536;

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

        return (AccessSettingsResponse)await access
            .SaveAsync(request.AccountsOnly.Value, By(), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>The LDAP directory: never the service account's password; the local names it has too.</summary>
    [HttpGet("ldap")]
    [ProducesResponseType<LdapSettingsResponse>(StatusCodes.Status200OK)]
    public async Task<LdapSettingsResponse> LdapAsync(CancellationToken cancellationToken)
    {
        LdapSettings settings = await ldap.GetAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<string> clashes = await ldap.ClashesAsync(settings, cancellationToken).ConfigureAwait(false);
        return LdapSettingsResponse.Of(settings, clashes);
    }

    /// <summary>Saves the LDAP directory, used from the next login; on, every field needed is checked.</summary>
    [HttpPut("ldap")]
    [Consumes("application/json")]
    [RequestSizeLimit(MaxLdapRequestBytes)]
    [ProducesResponseType<LdapSettingsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    public async Task<LdapSettingsResponse> SaveLdapAsync(
        LdapSettingsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        LdapSettings saved = await ldap
            .SaveAsync(request.ToChange(), By(), cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<string> clashes = await ldap.ClashesAsync(saved, cancellationToken).ConfigureAwait(false);
        return LdapSettingsResponse.Of(saved, clashes);
    }

    /// <summary>
    /// Tries the LDAP settings as typed, not saved: the connection, who a filter finds, or a name looked up (never a
    /// password). A directory that can't be reached is a result (<c>connected</c> false, saying why), not an error.
    /// </summary>
    [HttpPost("ldap/test")]
    [Consumes("application/json")]
    [RequestSizeLimit(MaxLdapRequestBytes)]
    [ProducesResponseType<LdapTestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    public async Task<LdapTestResponse> TestLdapAsync(LdapTestRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidationErrors errors = new();
        if (request.Test is null)
        {
            errors.Add("test", $"is required: one of {ApiValues.LdapTestKinds.Choices}.");
        }

        RequestRules.OneOf(errors, "test", request.Test, ApiValues.LdapTestKinds);
        errors.ThrowIfAny(RequestRules.Subject);
        LdapTestResult result = await ldap
            .TestAsync(request.ToChange(), ApiValues.LdapTestKinds[request.Test!], request.Username, cancellationToken)
            .ConfigureAwait(false);
        return (LdapTestResponse)result;
    }

    private string By() =>
        User.Identity?.Name ?? throw new InvalidOperationException("A logged-in request without a name.");
}
