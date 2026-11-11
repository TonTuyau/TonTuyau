using TonTuyau.Api.Errors;
using TonTuyau.Application.Accounts;

namespace TonTuyau.Api.Hosting;

/// <summary>
/// A library for logged-in accounts only (2.3.0): the browsing API, the pictures (<c>/art</c>), the files and streams
/// (<c>/media</c>) and the OpenAPI document answer 401 without an account's session. Always open: logging in
/// (<c>/api/auth</c>, which says whether the library is closed), the admin API (an admin's session already), the
/// browser extension's (its key), and <c>/health</c> (Docker's checks).
/// </summary>
internal sealed class AccountsOnlyGate(RequestDelegate next)
{
    private static readonly PathString[] Open = ["/api/auth", "/api/admin", "/api/extension", "/health"];

    private static readonly PathString[] Library = ["/api", "/art", "/media", "/openapi"];

    public async Task InvokeAsync(HttpContext context, AccessService access)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(access);
        PathString path = context.Request.Path;
        bool closable = Library.Any(path.StartsWithSegments) && !Open.Any(path.StartsWithSegments);
        if (closable && !AuthServices.IsAccount(context.User))
        {
            AccessSettings settings = await access.GetAsync(context.RequestAborted).ConfigureAwait(false);
            if (settings.AccountsOnly)
            {
                await Problems.WriteAsync(context, Problems.AccountsOnly()).ConfigureAwait(false);
                return;
            }
        }

        await next(context).ConfigureAwait(false);
    }
}
