using TonTuyau.Application.ApiKeys;

namespace TonTuyau.Infrastructure.ApiKeys;

/// <summary>A row of the <c>api_keys</c> table, as EF Core maps it. The application sees an <see cref="ApiKey"/>.</summary>
public sealed class ApiKeyRow
{
    public int Id { get; set; }

    public required string Name { get; set; }

    /// <summary>The name in lower case (invariant): unique, so two names that differ only by case can't both exist.</summary>
    public required string NameKey { get; set; }

    public required string Prefix { get; set; }

    /// <summary>The secret's SHA-256, hex (<see cref="ApiKeyService"/>), never the secret.</summary>
    public required string Hash { get; set; }

    /// <summary>Stored as its name (<c>BrowserExtension</c>).</summary>
    public ApiKeyKind Kind { get; set; }

    public bool Enabled { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public required string CreatedBy { get; set; }

    public DateTimeOffset? LastUsedAt { get; set; }

    public static explicit operator ApiKey(ApiKeyRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new ApiKey(
            row.Id,
            row.Name,
            row.Prefix,
            row.Kind,
            row.Enabled,
            row.CreatedAt,
            row.CreatedBy,
            row.LastUsedAt);
    }
}
