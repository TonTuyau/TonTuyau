using TonTuyau.Application.Accounts;

namespace TonTuyau.Api.Contracts;

/// <summary>The LDAP directory (contract v2.13); the service account's password never comes back.</summary>
/// <param name="BindPasswordSet">Whether the service account's password is stored.</param>
/// <param name="Clashes">The local accounts whose name the directory has too: the directory's can't log in.</param>
public sealed record LdapSettingsResponse(
    bool Enabled,
    string Url,
    bool StartTls,
    string? CaCertificate,
    string BindDn,
    bool BindPasswordSet,
    string BaseDn,
    string UidAttribute,
    string UsernameAttribute,
    string UsersFilter,
    string AdminsFilter,
    string PasswordAttribute,
    string? PasswordResetUrl,
    IReadOnlyList<string> Clashes,
    string? ChangedBy,
    DateTime? ChangedAt)
{
    public static LdapSettingsResponse Of(LdapSettings settings, IReadOnlyList<string> clashes)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new LdapSettingsResponse(
            settings.Enabled,
            settings.Url,
            settings.StartTls,
            settings.CaCertificate,
            settings.BindDn,
            !string.IsNullOrEmpty(settings.BindPassword),
            settings.BaseDn,
            settings.UidAttribute,
            settings.UsernameAttribute,
            settings.UsersFilter,
            settings.AdminsFilter,
            settings.PasswordAttribute,
            settings.PasswordResetUrl,
            clashes,
            settings.ChangedBy,
            settings.ChangedAt?.UtcDateTime);
    }
}
