using TonTuyau.Application.Accounts;

namespace TonTuyau.Infrastructure.Accounts;

/// <summary>
/// The row of the <c>access_settings</c> table (one at most, id 1), as EF Core maps it: who can watch (2.3.0). The
/// application sees <see cref="AccessSettings"/>.
/// </summary>
public sealed class AccessSettingsRow
{
    public const int OnlyId = 1;

    public int Id { get; set; } = OnlyId;

    public bool AccountsOnly { get; set; }

    public string? ChangedBy { get; set; }

    public DateTimeOffset? ChangedAt { get; set; }

    public static explicit operator AccessSettings(AccessSettingsRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new AccessSettings(row.AccountsOnly, row.ChangedBy, row.ChangedAt);
    }
}
