namespace TonTuyau.Application.Accounts;

/// <summary>
/// The LDAP directory TonTuyau asks who may log in (2.3.0), as Jellyfin's LDAP plugin takes it (the same values copy
/// over): a service account searches the accounts, a users filter and an admins filter say who logs in and as what.
/// </summary>
/// <param name="Url"><c>ldap://host:port</c> or <c>ldaps://host:port</c>.</param>
/// <param name="StartTls">An <c>ldap://</c> connection encrypted before anything is sent.</param>
/// <param name="CaCertificate">A certificate authority (PEM) to trust besides the system's; null: the system's.</param>
/// <param name="BindPassword">The service account's password; null while none is stored. Never sent to the page.</param>
/// <param name="BaseDn">Where the accounts are searched.</param>
/// <param name="UidAttribute">The name typed at login is this attribute's value (<c>uid</c>): the account's name.</param>
/// <param name="UsernameAttribute">The name shown (<c>cn</c>).</param>
/// <param name="UsersFilter">Who may log in, e.g. <c>(memberof=cn=tontuyau,ou=groups,dc=example,dc=com)</c>.</param>
/// <param name="AdminsFilter">The accounts that log in as admins.</param>
/// <param name="PasswordAttribute">
/// Written by the service account to change a password when the directory has no Password Modify operation.
/// </param>
/// <param name="PasswordResetUrl">The directory's page for a forgotten password, <c>$username</c> for the name.</param>
public sealed record LdapSettings(
    bool Enabled,
    string Url,
    bool StartTls,
    string? CaCertificate,
    string BindDn,
    string? BindPassword,
    string BaseDn,
    string UidAttribute,
    string UsernameAttribute,
    string UsersFilter,
    string AdminsFilter,
    string PasswordAttribute,
    string? PasswordResetUrl,
    string? ChangedBy,
    DateTimeOffset? ChangedAt)
{
    /// <summary>Off, with LLDAP's and OpenLDAP's usual attributes.</summary>
    public static readonly LdapSettings Default = new(
        Enabled: false,
        Url: "",
        StartTls: false,
        CaCertificate: null,
        BindDn: "",
        BindPassword: null,
        BaseDn: "",
        UidAttribute: "uid",
        UsernameAttribute: "cn",
        UsersFilter: "",
        AdminsFilter: "",
        PasswordAttribute: "userPassword",
        PasswordResetUrl: null,
        ChangedBy: null,
        ChangedAt: null);
}

/// <summary>The LDAP settings in TonTuyau's database (PostgreSQL): reads and writes, no rules.</summary>
public interface ILdapSettingsRepository
{
    /// <returns>The settings saved, or <see cref="LdapSettings.Default"/> while none are.</returns>
    Task<LdapSettings> GetAsync(CancellationToken cancellationToken);

    Task SaveAsync(LdapSettings settings, CancellationToken cancellationToken);
}
