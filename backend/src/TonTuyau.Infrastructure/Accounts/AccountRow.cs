using TonTuyau.Application.Accounts;

namespace TonTuyau.Infrastructure.Accounts;

/// <summary>A row of the <c>accounts</c> table, as EF Core maps it. The application sees an <see cref="Account"/>.</summary>
public sealed class AccountRow
{
    public int Id { get; set; }

    public required string Username { get; set; }

    /// <summary>The name in lower case (invariant): unique, so two names that differ only by case can't both exist.</summary>
    public required string UsernameKey { get; set; }

    public string? DisplayName { get; set; }

    /// <summary>Stored as its name (<c>Admin</c>, <c>User</c>).</summary>
    public AccountRole Role { get; set; }

    /// <summary>Stored as its name (<c>Local</c>, <c>Ldap</c>).</summary>
    public AccountSource Source { get; set; }

    public bool Disabled { get; set; }

    /// <summary>The PHC string of <see cref="Argon2idPasswordHasher"/>, never the password; null: the directory's.</summary>
    public string? PasswordHash { get; set; }

    public required string Stamp { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    public static explicit operator Account(AccountRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new Account(
            row.Id,
            row.Username,
            row.DisplayName,
            row.Role,
            row.Source,
            row.Disabled,
            row.PasswordHash,
            row.Stamp,
            row.CreatedAt,
            row.LastLoginAt);
    }

    /// <summary>The key a name is unique by: two names that differ only by case are the same.</summary>
    public static string KeyOf(string username) => username.ToLowerInvariant();
}
