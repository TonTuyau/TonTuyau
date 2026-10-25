using TonTuyau.Application.Accounts;

namespace TonTuyau.Api.Contracts;

/// <summary>What one LDAP test found (contract v2.13).</summary>
/// <param name="Error">Why it couldn't connect or log the service account in; null when connected.</param>
/// <param name="Found">The <c>admins</c> or <c>users</c> test: who that filter finds.</param>
/// <param name="User">The <c>name</c> test: the account looked up.</param>
public sealed record LdapTestResponse(
    bool Connected,
    string? Error,
    LdapFoundResponse? Found,
    LdapTestUserResponse? User)
{
    public static explicit operator LdapTestResponse(LdapTestResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        LdapFoundResponse? found = result.Found is null
            ? null
            : new LdapFoundResponse(
                result.FoundTotal ?? result.Found.Count,
                [.. result.Found.Select(f =>
                    new LdapFoundAccountResponse(f.Person.Username, f.Person.DisplayName, f.Clash))]);
        LdapTestUserResponse? user = result.Lookup is not { } lookup
            ? null
            : new LdapTestUserResponse(
                lookup.Person?.Dn,
                lookup.Person?.DisplayName,
                lookup.User,
                lookup.Admin,
                lookup.Role,
                lookup.Refused);
        return new LdapTestResponse(result.Connected, result.Error, found, user);
    }
}

/// <summary>Who a filter finds: how many, and the first 100 by name.</summary>
public sealed record LdapFoundResponse(int Total, IReadOnlyList<LdapFoundAccountResponse> Accounts);

/// <param name="Clash">A local account has that name: this one can't log in.</param>
public sealed record LdapFoundAccountResponse(string Username, string? DisplayName, bool Clash);

/// <summary>A name looked up: the account found (or null), each filter, the role it'd get, or why it's refused.</summary>
/// <param name="User">The users filter finds it.</param>
/// <param name="Admin">The admins filter finds it.</param>
public sealed record LdapTestUserResponse(
    string? Dn,
    string? DisplayName,
    bool User,
    bool Admin,
    AccountRole? Role,
    string? Refused);
