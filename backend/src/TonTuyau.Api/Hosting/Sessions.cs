using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using TonTuyau.Application.Accounts;
using TonTuyau.Application.Browse;

namespace TonTuyau.Api.Hosting;

/// <summary>
/// A session's cookie (2.3.0): who it is (the account's id, name, role and stamp), made at login, and checked again
/// every few minutes: an account gone, disabled, or whose password, role or state changed since (a new stamp) ends it.
/// </summary>
internal static partial class Sessions
{
    /// <summary>The account's stamp when the session was made (<see cref="Account.Stamp"/>).</summary>
    public const string StampClaim = "tontuyau:stamp";

    /// <summary>When the session was last checked, in its cookie's properties (ISO 8601, UTC).</summary>
    private const string CheckedItem = "tontuyau:checked";

    /// <summary>How often a session is checked against its account.</summary>
    public static readonly TimeSpan CheckEvery = TimeSpan.FromMinutes(5);

    /// <summary>The account as its session's claims, for the cookie.</summary>
    public static ClaimsPrincipal PrincipalOf(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);
        ClaimsIdentity identity = new(
            [
                new Claim(ClaimTypes.NameIdentifier, account.Id.ToString(CultureInfo.InvariantCulture)),
                new Claim(ClaimTypes.Name, account.Username),
                new Claim(ClaimTypes.Role, ApiValues.AccountRoles.NameOf(account.Role)),
                new Claim(StampClaim, account.Stamp),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    /// <summary>Notes that the session was checked now (at login, it is).</summary>
    public static void MarkChecked(AuthenticationProperties properties, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(properties);
        properties.Items[CheckedItem] = at.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
    }

    /// <summary>The logged-in account's id, or null (an API key, nobody).</summary>
    public static int? AccountIdOf(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        string? id = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out int value) ? value : null;
    }

    /// <summary>
    /// The cookie handler's check: every <see cref="CheckEvery"/>, the account is read again; a session it no longer
    /// allows (or one from before 2.3.0, without a stamp) is refused, and its cookie deleted.
    /// </summary>
    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        TimeProvider clock = context.HttpContext.RequestServices.GetRequiredService<TimeProvider>();
        DateTimeOffset now = clock.GetUtcNow();
        bool checkedLately = context.Properties.Items.TryGetValue(CheckedItem, out string? at)
            && DateTimeOffset.TryParse(
                at,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out DateTimeOffset when)
            && now - when < CheckEvery;
        if (checkedLately)
        {
            return;
        }

        ClaimsPrincipal? user = context.Principal;
        int? id = user is null ? null : AccountIdOf(user);
        Account? account = id is null
            ? null
            : await context.HttpContext.RequestServices
                .GetRequiredService<AccountService>()
                .SessionAsync(id.Value, user!.FindFirstValue(StampClaim), context.HttpContext.RequestAborted)
                .ConfigureAwait(false);
        if (account is null)
        {
            ILogger logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(Sessions));
            LogSessionEnded(logger, user?.Identity?.Name ?? "?");
            context.RejectPrincipal();
            await context.HttpContext
                .SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme)
                .ConfigureAwait(false);
            return;
        }

        MarkChecked(context.Properties, now);
        context.ShouldRenew = true;
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Ended a session of {Username}: its account is gone, disabled or changed since.")]
    private static partial void LogSessionEnded(ILogger logger, string username);
}
