using TonTuyau.Application.Accounts;

namespace TonTuyau.Infrastructure.Accounts;

/// <summary>
/// The row of the <c>ldap_settings</c> table (one at most, id 1), as EF Core maps it: the LDAP directory (2.3.0). The
/// application sees <see cref="LdapSettings"/>.
/// </summary>
public sealed class LdapSettingsRow
{
    public const int OnlyId = 1;

    public int Id { get; set; } = OnlyId;

    public bool Enabled { get; set; }

    public required string Url { get; set; }

    public bool StartTls { get; set; }

    public string? CaCertificate { get; set; }

    public required string BindDn { get; set; }

    /// <summary>The service account's password, as the YouTube cookie is kept: never logged, never sent to the page.</summary>
    public string? BindPassword { get; set; }

    public required string BaseDn { get; set; }

    public required string UidAttribute { get; set; }

    public required string UsernameAttribute { get; set; }

    public required string UsersFilter { get; set; }

    public required string AdminsFilter { get; set; }

    public required string PasswordAttribute { get; set; }

    public string? PasswordResetUrl { get; set; }

    public string? ChangedBy { get; set; }

    public DateTimeOffset? ChangedAt { get; set; }

    public static explicit operator LdapSettings(LdapSettingsRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new LdapSettings(
            row.Enabled,
            row.Url,
            row.StartTls,
            row.CaCertificate,
            row.BindDn,
            row.BindPassword,
            row.BaseDn,
            row.UidAttribute,
            row.UsernameAttribute,
            row.UsersFilter,
            row.AdminsFilter,
            row.PasswordAttribute,
            row.PasswordResetUrl,
            row.ChangedBy,
            row.ChangedAt);
    }
}
