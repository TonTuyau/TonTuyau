using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using TonTuyau.Api.Errors;
using TonTuyau.Application.ApiKeys;

namespace TonTuyau.Api.Hosting;

/// <summary>
/// Signs a request in with the API key it brings (<c>Authorization: Bearer tt_…</c>, 1.5), named after its key
/// ("Firefox, laptop (API key)") and carrying its kind: only the endpoints that take that kind let it in
/// (<see cref="AuthServices"/>'s policies). Only asked when the request has a Bearer header.
/// </summary>
internal sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ApiKeyService keys) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string BearerPrefix = "Bearer ";

    /// <summary>The claim holding the key's id: a request it signed in carries it.</summary>
    public const string KeyIdClaim = "tontuyau:api_key";

    /// <summary>The claim holding the key's kind (<see cref="ApiKeyKind"/>'s name): a session has none.</summary>
    public const string KindClaim = "tontuyau:api_key_kind";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        ApiKey? key = await keys
            .AuthenticateAsync(header[BearerPrefix.Length..].Trim(), Context.RequestAborted)
            .ConfigureAwait(false);
        if (key is null)
        {
            return AuthenticateResult.Fail("Unknown or disabled API key.");
        }

        ClaimsIdentity identity = new(
            [
                new Claim(ClaimTypes.Name, $"{key.Name} (API key)"),
                new Claim(KeyIdClaim, key.Id.ToString(CultureInfo.InvariantCulture)),
                new Claim(KindClaim, key.Kind.ToString()),
            ],
            SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    /// <summary>401 with the API's problem, and the scheme to use: the same answer for an unknown or a disabled key.</summary>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Bearer";
        return Problems.WriteAsync(Context, Problems.ApiKeyRefused());
    }

    /// <summary>403: the key is fine, but its kind doesn't open this endpoint.</summary>
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        Problems.WriteAsync(Context, Problems.ApiKeyNotAllowed());
}
