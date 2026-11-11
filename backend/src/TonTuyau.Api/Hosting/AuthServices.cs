using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using TonTuyau.Api.Errors;
using TonTuyau.Application.Accounts;
using TonTuyau.Application.ApiKeys;
using TonTuyau.Application.Browse;
using TonTuyau.Application.Configuration;
using TonTuyau.Infrastructure;
using TonTuyau.Infrastructure.Accounts;

namespace TonTuyau.Api.Hosting;

/// <summary>
/// Logging in: the accounts' database, ASP.NET Core's cookie authentication and the login's rate limit; API keys
/// (1.5) for programs. Web host only (the CLI never needs an account).
/// </summary>
internal static class AuthServices
{
    public const string LoginRateLimit = "login";

    /// <summary>One's own password changed (2.3.0): its current password guessed, as a login's; counted apart.</summary>
    public const string PasswordRateLimit = "password";

    /// <summary>The default scheme: a request with a Bearer header is an API key's, any other the session cookie's.</summary>
    public const string SessionOrApiKey = "SessionOrApiKey";

    /// <summary>
    /// The policy of the endpoints the browser extension calls: a logged-in admin, or a <c>browserExtension</c> key.
    /// Every other <c>[Authorize]</c> endpoint takes the default policy: a logged-in admin only, never a key.
    /// </summary>
    public const string BrowserExtensionPolicy = "BrowserExtension";

    /// <summary>
    /// The policy of what any logged-in account may do (2.3.0: its own password), an admin or a user; never a key.
    /// </summary>
    public const string AccountPolicy = "Account";
    public const string CookieName = "tontuyau_session";
    public const int LoginAttemptsPerMinute = 5;
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);
    public static readonly TimeSpan RememberedLifetime = TimeSpan.FromDays(30);

    public static IServiceCollection AddTonTuyauAuth(this IServiceCollection services)
    {
        services.AddAccounts();
        services.AddSingleton(sp => new ClientAddresses(
            ClientAddresses.Parse(sp.GetRequiredService<TonTuyauOptions>().Accounts.TrustedProxies)));

        // The keys that sign the cookies live in PostgreSQL: a restart, or a new container, logs nobody out. They're
        // stored unencrypted (Data Protection warns about it once, when it makes a key): the database is the stack's
        // own, never published.
        services
            .AddDataProtection()
            .SetApplicationName("TonTuyau")
            .PersistKeysToDbContext<TonTuyauDbContext>();

        services
            .AddAuthentication(SessionOrApiKey)
            .AddCookie()
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationHandler.SchemeName,
                _ => { })
            .AddPolicyScheme(SessionOrApiKey, SessionOrApiKey, o => o.ForwardDefaultSelector = SchemeOf);
        services
            .AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<TonTuyauOptions>(ConfigureCookie);
        // Default deny for keys and (2.3.0) users: [Authorize] alone is an admin's session; an endpoint open to a kind
        // of key, or to any account, says so.
        AuthorizationPolicy adminOnly = new AuthorizationPolicyBuilder()
            .RequireAssertion(context => IsAdmin(context.User))
            .Build();
        services
            .AddAuthorizationBuilder()
            .SetDefaultPolicy(adminOnly)
            .AddPolicy(BrowserExtensionPolicy, policy => policy.RequireAssertion(IsAdminOrBrowserExtension))
            .AddPolicy(AccountPolicy, policy => policy.RequireAssertion(context => IsAccount(context.User)));

        // Guessing passwords: a few attempts per minute and client address, then 429 (as ProblemDetails, by the
        // status code pages).
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(LoginRateLimit, PerClientAddress);
            o.AddPolicy(PasswordRateLimit, PerClientAddress);
        });
        return services;
    }

    /// <summary>Logged in with the session (an account, an admin or a user): not an API key.</summary>
    public static bool IsAccount(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true && !user.HasClaim(c => c.Type == ApiKeyAuthenticationHandler.KindClaim);

    /// <summary>An admin's session (2.3.0: the role in it).</summary>
    private static bool IsAdmin(ClaimsPrincipal user) =>
        IsAccount(user) && user.IsInRole(ApiValues.AccountRoles.NameOf(AccountRole.Admin));

    private static bool IsAdminOrBrowserExtension(AuthorizationHandlerContext context) =>
        IsAdmin(context.User) || IsKeyOfKind(context.User, ApiKeyKind.BrowserExtension);

    private static bool IsKeyOfKind(ClaimsPrincipal user, ApiKeyKind kind) =>
        user.HasClaim(ApiKeyAuthenticationHandler.KindClaim, kind.ToString());

    private static string SchemeOf(HttpContext context) =>
        context.Request.Headers.Authorization
            .ToString()
            .StartsWith(ApiKeyAuthenticationHandler.BearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? ApiKeyAuthenticationHandler.SchemeName
            : CookieAuthenticationDefaults.AuthenticationScheme;

    /// <summary>Per client address (<see cref="ClientAddresses"/>: the visitor's, behind a named proxy too).</summary>
    private static RateLimitPartition<string> PerClientAddress(HttpContext context) =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.RequestServices
                .GetRequiredService<ClientAddresses>()
                .Of(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = LoginAttemptsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });

    private static void ConfigureCookie(CookieAuthenticationOptions cookie, TonTuyauOptions options)
    {
        cookie.Cookie.Name = CookieName;
        cookie.Cookie.HttpOnly = true;
        // Never sent with a request another site starts: with the JSON-only login (a cross-site form can't post
        // JSON), that's the protection against cross-site request forgery here.
        cookie.Cookie.SameSite = SameSiteMode.Strict;
        // The backend always sees plain HTTP (from the front server): the HTTPS is the reverse proxy's, so the flag
        // can't follow the request's scheme.
        cookie.Cookie.SecurePolicy = options.Accounts.CookieSecure
            ? CookieSecurePolicy.Always
            : CookieSecurePolicy.None;
        // 8 hours, renewed while in use; "Stay logged in" makes it 30 days (AuthController).
        cookie.ExpireTimeSpan = SessionLifetime;
        cookie.SlidingExpiration = true;

        // 2.3.0: checked against its account every few minutes (Sessions).
        cookie.Events.OnValidatePrincipal = Sessions.ValidateAsync;

        // An API answers 401 / 403, not the cookie handler's redirect to a login page: the front has its own.
        cookie.Events.OnRedirectToLogin = context => Problems.WriteAsync(context.HttpContext, Problems.NotLoggedIn());
        cookie.Events.OnRedirectToAccessDenied = context =>
            Problems.WriteAsync(context.HttpContext, Problems.ForStatus(StatusCodes.Status403Forbidden));
    }
}
