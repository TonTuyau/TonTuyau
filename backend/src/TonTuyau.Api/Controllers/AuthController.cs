using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TonTuyau.Api.Contracts;
using TonTuyau.Api.Errors;
using TonTuyau.Api.Hosting;
using TonTuyau.Application.Accounts;

namespace TonTuyau.Api.Controllers;

/// <summary>
/// Logging in and out. The session is ASP.NET Core's cookie (HttpOnly, SameSite=Strict): the page never sees it, it
/// asks <c>me</c> who is logged in.
/// </summary>
[ApiController]
[Route("api/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed partial class AuthController(
    AccountService accounts,
    ClientAddresses clients,
    TimeProvider clock,
    ILogger<AuthController> logger) : ControllerBase
{
    /// <summary>The largest login body taken: a name, a password and a flag fit in far less.</summary>
    public const int MaxLoginBytes = 4096;

    /// <summary>
    /// Logs in: 204 and the session cookie. Any refusal is the same 401 with the same message, whether the name
    /// exists or not. JSON only (415 otherwise): a cross-site form can't send it.
    /// </summary>
    [HttpPost("login")]
    [Consumes("application/json")]
    [RequestSizeLimit(MaxLoginBytes)]
    [EnableRateLimiting(AuthServices.LoginRateLimit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, Problems.ContentType)]
    public async Task<IActionResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Account? account = await accounts
            .LoginAsync(request.Username, request.Password, cancellationToken)
            .ConfigureAwait(false);
        if (account is null)
        {
            string address = clients.Of(HttpContext);
            LogRefused(address);
            return new ProblemResult(Problems.LoginRefused());
        }

        AuthenticationProperties properties = new()
        {
            // A persistent cookie survives closing the browser; otherwise it's a session cookie (8 h at most).
            IsPersistent = request.Remember,
            ExpiresUtc = request.Remember ? clock.GetUtcNow() + AuthServices.RememberedLifetime : null,
        };
        Sessions.MarkChecked(properties, clock.GetUtcNow());
        await HttpContext
            .SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, Sessions.PrincipalOf(account), properties)
            .ConfigureAwait(false);
        LogLoggedIn(account.Username);
        return NoContent();
    }

    /// <summary>Logs out: 204, and the browser drops the session cookie.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LogoutAsync()
    {
        await HttpContext
            .SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            .ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>Who is logged in (2.3.0: with the role, where from, the directory's name), or 401.</summary>
    [HttpGet("me")]
    [ProducesResponseType<MeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, Problems.ContentType)]
    public async Task<IActionResult> MeAsync(CancellationToken cancellationToken)
    {
        // A session only (an API key has no account); its cookie was checked against the account a moment ago at most.
        int? id = User.Identity?.AuthenticationType == CookieAuthenticationDefaults.AuthenticationScheme
            ? Sessions.AccountIdOf(User)
            : null;
        Account? account = id is null
            ? null
            : await accounts.FindAsync(id.Value, cancellationToken).ConfigureAwait(false);
        return account is null ? new ProblemResult(Problems.NotLoggedIn()) : Ok((MeResponse)account);
    }

    /// <summary>
    /// A local account changes its own password (2.3.0, My account): 204, and this session goes on (the account's other
    /// sessions end at their next check). 400 when the current password is wrong or the new one doesn't fit; 409 for a
    /// directory account.
    /// </summary>
    [HttpPut("me/password")]
    [Authorize(Policy = AuthServices.AccountPolicy)]
    [Consumes("application/json")]
    [RequestSizeLimit(MaxLoginBytes)]
    [EnableRateLimiting(AuthServices.PasswordRateLimit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, Problems.ContentType)]
    public async Task<IActionResult> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        int id = Sessions.AccountIdOf(User) ?? throw new InvalidOperationException("A session without an account.");
        Account account = await accounts
            .ChangeOwnPasswordAsync(id, request.Current, request.Password, cancellationToken)
            .ConfigureAwait(false);
        // This session goes on, with the new stamp; as long-lived as it was.
        AuthenticateResult current = await HttpContext
            .AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            .ConfigureAwait(false);
        AuthenticationProperties properties = current.Properties ?? new AuthenticationProperties();
        Sessions.MarkChecked(properties, clock.GetUtcNow());
        await HttpContext
            .SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, Sessions.PrincipalOf(account), properties)
            .ConfigureAwait(false);
        return NoContent();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Username} logged in.")]
    private partial void LogLoggedIn(string username);

    // Not the name: a password typed in the wrong box would end up in the log.
    [LoggerMessage(Level = LogLevel.Information, Message = "Refused a login from {Address}.")]
    private partial void LogRefused(string address);
}
