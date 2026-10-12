using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
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
            string address = AuthServices.ClientAddress(HttpContext);
            LogRefused(address);
            return new ProblemResult(Problems.LoginRefused());
        }

        ClaimsIdentity identity = new(
            [
                new Claim(ClaimTypes.NameIdentifier, account.Id.ToString(CultureInfo.InvariantCulture)),
                new Claim(ClaimTypes.Name, account.Username),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);
        AuthenticationProperties properties = new()
        {
            // A persistent cookie survives closing the browser; otherwise it's a session cookie (8 h at most).
            IsPersistent = request.Remember,
            ExpiresUtc = request.Remember ? clock.GetUtcNow() + AuthServices.RememberedLifetime : null,
        };
        await HttpContext
            .SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), properties)
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

    /// <summary>Who is logged in, or 401.</summary>
    [HttpGet("me")]
    [ProducesResponseType<MeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, Problems.ContentType)]
    public IActionResult Me() =>
        User.Identity is { IsAuthenticated: true, Name: { } username }
            ? Ok(new MeResponse(username))
            : new ProblemResult(Problems.NotLoggedIn());

    [LoggerMessage(Level = LogLevel.Information, Message = "{Username} logged in.")]
    private partial void LogLoggedIn(string username);

    // Not the name: a password typed in the wrong box would end up in the log.
    [LoggerMessage(Level = LogLevel.Information, Message = "Refused a login from {Address}.")]
    private partial void LogRefused(string address);
}
