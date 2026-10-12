using TonTuyau.Application.Accounts;

namespace TonTuyau.Infrastructure.Accounts;

/// <summary>A row of the <c>accounts</c> table, as EF Core maps it. The application sees an <see cref="Account"/>.</summary>
public sealed class AccountRow
{
    public int Id { get; set; }

    public required string Username { get; set; }

    /// <summary>The PHC string of <see cref="Argon2idPasswordHasher"/>, never the password.</summary>
    public required string PasswordHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    public static explicit operator Account(AccountRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new Account(row.Id, row.Username, row.PasswordHash, row.CreatedAt, row.LastLoginAt);
    }
}
