using TonTuyau.Application.Accounts;

namespace TonTuyau.Api.Contracts;

/// <summary>
/// A test of the LDAP settings as typed (contract v2.13), not saved: the same fields as <see cref="LdapSettingsRequest"/>,
/// the test, and for <c>name</c> the name to look up (never a password).
/// </summary>
/// <param name="Test"><c>connection</c>, <c>admins</c>, <c>users</c> or <c>name</c>.</param>
public sealed record LdapTestRequest(
    string? Test = null,
    string? Username = null,
    string? Url = null,
    bool StartTls = false,
    string? CaCertificate = null,
    string? BindDn = null,
    string? BindPassword = null,
    string? BaseDn = null,
    string? UidAttribute = null,
    string? UsernameAttribute = null,
    string? UsersFilter = null,
    string? AdminsFilter = null,
    string? PasswordAttribute = null,
    string? PasswordResetUrl = null)
{
    public LdapSettingsChange ToChange() =>
        new(
            Enabled: true,
            Url,
            StartTls,
            CaCertificate,
            BindDn,
            BindPassword,
            BaseDn,
            UidAttribute,
            UsernameAttribute,
            UsersFilter,
            AdminsFilter,
            PasswordAttribute,
            PasswordResetUrl);
}
