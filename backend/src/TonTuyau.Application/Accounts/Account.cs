namespace TonTuyau.Application.Accounts;

/// <summary>What an account may do (2.3.0): everything, or watch the library.</summary>
public enum AccountRole
{
    Admin,
    User,
}

/// <summary>Where an account is from (2.3.0): made here by an admin (or the first start), or the LDAP directory.</summary>
public enum AccountSource
{
    Local,
    Ldap,
}

/// <summary>Someone who can log in.</summary>
/// <param name="Username">Unique whatever the case (2.3.0), compared as typed at login (case matters).</param>
/// <param name="DisplayName">The directory's name for it (2.3.0); null for a local account.</param>
/// <param name="Disabled">Refused at login, and its sessions end at their next check (2.3.0).</param>
/// <param name="PasswordHash">
/// What <see cref="IPasswordHasher"/> made of the password, never the password; null for a directory account (its
/// password is the directory's).
/// </param>
/// <param name="Stamp">
/// Changes with the password, the role or <paramref name="Disabled"/> (2.3.0): a session made before it ends at its next
/// check.
/// </param>
public sealed record Account(
    int Id,
    string Username,
    string? DisplayName,
    AccountRole Role,
    AccountSource Source,
    bool Disabled,
    string? PasswordHash,
    string Stamp,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);
