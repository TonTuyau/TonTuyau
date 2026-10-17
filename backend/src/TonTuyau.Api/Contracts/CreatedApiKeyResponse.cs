using TonTuyau.Application.ApiKeys;

namespace TonTuyau.Api.Contracts;

/// <summary>A key just created.</summary>
/// <param name="Secret">
/// What a program sends as <c>Authorization: Bearer &lt;secret&gt;</c>. Given this once: TonTuyau keeps only its hash.
/// </param>
public sealed record CreatedApiKeyResponse(ApiKeyResponse Key, string Secret)
{
    public static explicit operator CreatedApiKeyResponse(CreatedApiKey created)
    {
        ArgumentNullException.ThrowIfNull(created);
        return new CreatedApiKeyResponse((ApiKeyResponse)created.Key, created.Secret);
    }
}
