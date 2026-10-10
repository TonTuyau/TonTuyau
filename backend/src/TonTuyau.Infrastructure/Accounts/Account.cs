namespace TonTuyau.Infrastructure.Accounts;

/// <summary>A row of the <c>accounts</c> table: someone who can log in.</summary>
public sealed class Account
{
    public int Id { get; set; }

    /// <summary>Unique, compared as typed (case matters).</summary>
    public required string Username { get; set; }

    /// <summary>The PHC string of <see cref="PasswordHasher"/>, never the password.</summary>
    public required string PasswordHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }
}
