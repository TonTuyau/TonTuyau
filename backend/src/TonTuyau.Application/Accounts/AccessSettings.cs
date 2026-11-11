namespace TonTuyau.Application.Accounts;

/// <summary>Who can watch (2.3.0): everyone who reaches TonTuyau (the default), or the logged-in accounts only.</summary>
/// <param name="ChangedBy">The admin who set it last; null while it's the default.</param>
public sealed record AccessSettings(bool AccountsOnly, string? ChangedBy, DateTimeOffset? ChangedAt)
{
    public static readonly AccessSettings Default = new(AccountsOnly: false, ChangedBy: null, ChangedAt: null);
}

/// <summary>Who can watch, in TonTuyau's database (PostgreSQL): reads and writes, no rules.</summary>
public interface IAccessSettingsRepository
{
    /// <returns>The settings saved, or <see cref="AccessSettings.Default"/> while none are.</returns>
    Task<AccessSettings> GetAsync(CancellationToken cancellationToken);

    Task SaveAsync(AccessSettings settings, CancellationToken cancellationToken);
}
