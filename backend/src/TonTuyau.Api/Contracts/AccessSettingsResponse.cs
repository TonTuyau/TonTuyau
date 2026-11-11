using TonTuyau.Application.Accounts;

namespace TonTuyau.Api.Contracts;

/// <summary>Who can watch (contract v2.13): everyone (the default), or the logged-in accounts only.</summary>
/// <param name="ChangedBy">The admin who set it last; null while it's the default.</param>
public sealed record AccessSettingsResponse(bool AccountsOnly, string? ChangedBy, DateTime? ChangedAt)
{
    public static explicit operator AccessSettingsResponse(AccessSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new AccessSettingsResponse(settings.AccountsOnly, settings.ChangedBy, settings.ChangedAt?.UtcDateTime);
    }
}
