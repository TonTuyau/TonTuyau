namespace TonTuyau.Api.Contracts;

/// <summary>
/// Whether the library is for logged-in accounts only (contract v2.13), open to anyone: the page shows the login page
/// then.
/// </summary>
/// <param name="PasswordResetUrl">
/// The LDAP directory's page for a forgotten password (<c>$username</c> for the name typed), linked from the login
/// page; null when the directory is off or has none.
/// </param>
public sealed record AccessResponse(bool AccountsOnly, string? PasswordResetUrl);
