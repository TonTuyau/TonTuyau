namespace TonTuyau.Application.Accounts;

/// <summary>Someone who can log in.</summary>
/// <param name="Username">Unique, compared as typed (case matters).</param>
/// <param name="PasswordHash">What <see cref="IPasswordHasher"/> made of the password, never the password.</param>
public sealed record Account(
    int Id,
    string Username,
    string PasswordHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);
