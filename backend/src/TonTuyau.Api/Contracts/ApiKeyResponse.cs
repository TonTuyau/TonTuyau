using TonTuyau.Application.ApiKeys;

namespace TonTuyau.Api.Contracts;

/// <summary>An API key, without its secret (only its hash is kept).</summary>
/// <param name="Prefix">The secret's first characters (<c>tt_k7Qm</c>), to tell keys apart.</param>
/// <param name="Kind">What it may do: <c>browserExtension</c> (the extension's endpoints only).</param>
/// <param name="CreatedBy">The account that created it.</param>
/// <param name="LastUsedAt">Its last request, to the minute; null if never used.</param>
public sealed record ApiKeyResponse(
    int Id,
    string Name,
    string Prefix,
    ApiKeyKind Kind,
    bool Enabled,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? LastUsedAt)
{
    public static explicit operator ApiKeyResponse(ApiKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return new ApiKeyResponse(
            key.Id,
            key.Name,
            key.Prefix,
            key.Kind,
            key.Enabled,
            key.CreatedAt.UtcDateTime,
            key.CreatedBy,
            key.LastUsedAt?.UtcDateTime);
    }
}
