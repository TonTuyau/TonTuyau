using TonTuyau.Application.Accounts;

namespace TonTuyau.Api.Contracts;

/// <summary>Who is logged in.</summary>
/// <param name="DisplayName">The directory's name for the account (contract v2.13); null for a local one.</param>
/// <param name="Role">An admin (everything) or a user (watches the library) (contract v2.13).</param>
/// <param name="Source">Made here, or the LDAP directory's (contract v2.13).</param>
public sealed record MeResponse(string Username, string? DisplayName, AccountRole Role, AccountSource Source)
{
    public static explicit operator MeResponse(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);
        return new MeResponse(account.Username, account.DisplayName, account.Role, account.Source);
    }
}
