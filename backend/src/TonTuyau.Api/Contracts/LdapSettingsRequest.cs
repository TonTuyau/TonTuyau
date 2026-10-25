using TonTuyau.Application.Accounts;

namespace TonTuyau.Api.Contracts;

/// <summary>
/// The LDAP directory (contract v2.13), as Jellyfin's LDAP plugin takes it. On, every field is needed but the CA
/// certificate and the password reset URL; off, anything is kept as it is.
/// </summary>
/// <param name="Url"><c>ldap://host:port</c> or <c>ldaps://host:port</c>.</param>
/// <param name="CaCertificate">A certificate authority (PEM) to trust besides the system's; null: the system's.</param>
/// <param name="BindPassword">The service account's password; absent or empty: the one stored is kept.</param>
/// <param name="UidAttribute">The name typed at login (<c>uid</c>).</param>
/// <param name="UsernameAttribute">The name shown (<c>cn</c>).</param>
/// <param name="UsersFilter">Who may log in, e.g. <c>(memberof=cn=tontuyau,ou=groups,dc=example,dc=com)</c>.</param>
/// <param name="AdminsFilter">The accounts that log in as admins.</param>
/// <param name="PasswordAttribute">Written when the directory has no Password Modify operation.</param>
/// <param name="PasswordResetUrl">The directory's page for a forgotten password, <c>$username</c> for the name.</param>
public sealed record LdapSettingsRequest(
    bool Enabled = false,
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
            Enabled,
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
