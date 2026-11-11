using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TonTuyau.Api.Contracts;
using TonTuyau.Api.Errors;
using TonTuyau.Api.Hosting;
using TonTuyau.Application.Accounts;

namespace TonTuyau.Api.Controllers;

/// <summary>
/// The admin's accounts page (2.3.0): every account, a local one made, an account changed (its role, its password,
/// disabled or not) or deleted. An admin's session only (the default policy). The last enabled local admin stays, and
/// nobody disables nor deletes their own account (409).
/// </summary>
[ApiController]
[Authorize]
[Route("api/admin/accounts")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, Problems.ContentType)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, Problems.ContentType)]
public sealed class AccountsController(AccountService accounts) : ControllerBase
{
    /// <summary>The largest body taken: a name, a password and a role fit in far less.</summary>
    public const int MaxRequestBytes = 4096;

    /// <summary>Every account, local or the directory's, by name.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AccountResponse>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<AccountResponse>> ListAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Account> list = await accounts
            .ListAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. list.Select(a => (AccountResponse)a)];
    }

    /// <summary>Makes a local account, enabled: 201. 409 when an account has the name already (whatever the case).</summary>
    [HttpPost]
    [Consumes("application/json")]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, Problems.ContentType)]
    public async Task<IActionResult> CreateAsync(NewAccountRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Account account = await accounts
            .CreateAsync(request.Username, request.Password, request.Role, By(), cancellationToken)
            .ConfigureAwait(false);
        return StatusCode(StatusCodes.Status201Created, (AccountResponse)account);
    }

    /// <summary>
    /// Changes an account: what's given (its role, its password, disabled or not; a directory one's state only). Its
    /// sessions end within minutes.
    /// </summary>
    [HttpPut("{id:int}")]
    [Consumes("application/json")]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, Problems.ContentType)]
    public async Task<AccountResponse> ChangeAsync(
        int id,
        AccountChangeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return (AccountResponse)await accounts
            .ChangeAsync(id, request.Role, request.Password, request.Disabled, ById(), By(), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Deletes an account for good (a directory one comes back at its next login).</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, Problems.ContentType)]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        await accounts
            .DeleteAsync(id, ById(), By(), cancellationToken)
            .ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>The logged-in admin's name ([Authorize]: there is one).</summary>
    private string By() =>
        User.Identity?.Name ?? throw new InvalidOperationException("A logged-in request without a name.");

    private int ById() =>
        Sessions.AccountIdOf(User) ?? throw new InvalidOperationException("An admin's session without an account.");
}
