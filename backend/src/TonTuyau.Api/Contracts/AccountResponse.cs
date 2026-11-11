using TonTuyau.Application.Accounts;

namespace TonTuyau.Api.Contracts;

/// <summary>An account, as the admin's accounts page lists it (contract v2.13): never its password.</summary>
/// <param name="DisplayName">The directory's name for it; null for a local one.</param>
/// <param name="Role">An admin (everything) or a user (watches the library); a directory account's is its groups'.</param>
/// <param name="Source">Made here (<c>local</c>), or the LDAP directory's (<c>ldap</c>, made at its first login).</param>
/// <param name="Disabled">Refused at login; its sessions end within minutes.</param>
/// <param name="LastLoginAt">Its last login; null if never.</param>
public sealed record AccountResponse(
    int Id,
    string Username,
    string? DisplayName,
    AccountRole Role,
    AccountSource Source,
    bool Disabled,
    DateTime CreatedAt,
    DateTime? LastLoginAt)
{
    public static explicit operator AccountResponse(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);
        return new AccountResponse(
            account.Id,
            account.Username,
            account.DisplayName,
            account.Role,
            account.Source,
            account.Disabled,
            account.CreatedAt.UtcDateTime,
            account.LastLoginAt?.UtcDateTime);
    }
}
