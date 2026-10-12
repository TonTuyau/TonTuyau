using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using TonTuyau.Application.Configuration;
using TonTuyau.Infrastructure;
using TonTuyau.Infrastructure.Accounts;

namespace TonTuyau.Api.Hosting;

/// <summary>
/// Logging in: the accounts' database, ASP.NET Core's cookie authentication and the login's rate limit. Web host
/// only (the CLI never needs an account).
/// </summary>
internal static class AuthServices
{
    public const string LoginRateLimit = "login";
    public const string CookieName = "tontuyau_session";
    public const int LoginAttemptsPerMinute = 5;
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);
    public static readonly TimeSpan RememberedLifetime = TimeSpan.FromDays(30);

    public static IServiceCollection AddTonTuyauAuth(this IServiceCollection services)
    {
        services.AddAccounts();

        // The keys that sign the cookies live in PostgreSQL: a restart, or a new container, logs nobody out. They're
        // stored unencrypted (Data Protection warns about it once, when it makes a key): the database is the stack's
        // own, never published.
        services
            .AddDataProtection()
            .SetApplicationName("TonTuyau")
            .PersistKeysToDbContext<TonTuyauDbContext>();

        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie();
        services
            .AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<TonTuyauOptions>(ConfigureCookie);

        // Guessing passwords: a few attempts per minute and client address, then 429 (as ProblemDetails, by the
        // status code pages).
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(LoginRateLimit, PerClientAddress);
        });
        return services;
    }

    /// <summary>
    /// Who is asking, for the rate limit. The backend is only reached through the front server, which adds the address
    /// it got the request from at the end of <c>X-Forwarded-For</c>: that last entry is the one to trust (what comes
    /// before it, the client or a proxy in between could have written). Behind a reverse proxy it's the proxy's
    /// address, so every client shares the limit: README, "Logging in".
    /// </summary>
    internal static string ClientAddress(HttpContext context)
    {
        string? forwarded = context.Request.Headers["X-Forwarded-For"]
            .LastOrDefault()
            ?.Split(',')[^1]
            .Trim();
        return forwarded is { Length: > 0 } ? forwarded : context.Connection.RemoteIpAddress?.ToString() ?? "";
    }

    private static RateLimitPartition<string> PerClientAddress(HttpContext context) =>
        RateLimitPartition.GetFixedWindowLimiter(
            ClientAddress(context),
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
    }
}
