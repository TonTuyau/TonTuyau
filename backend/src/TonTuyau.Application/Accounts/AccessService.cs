using Microsoft.Extensions.Logging;

namespace TonTuyau.Application.Accounts;

/// <summary>
/// Who can watch (2.3.0): read at every request to the library (its pages' API, its pictures, its files), so kept here
/// once read, and again after a minute at most; an admin's change is seen at once.
/// </summary>
public sealed partial class AccessService(
    IAccessSettingsRepository settings,
    TimeProvider clock,
    ILogger<AccessService> logger)
{
    /// <summary>How long a value read is kept before the database is asked again.</summary>
    public static readonly TimeSpan KeptFor = TimeSpan.FromMinutes(1);

    private readonly Lock _gate = new();
    private (AccessSettings Settings, DateTimeOffset ReadAt)? _kept;

    public async Task<AccessSettings> GetAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_kept is { } kept && clock.GetUtcNow() - kept.ReadAt < KeptFor)
            {
                return kept.Settings;
            }
        }

        AccessSettings read = await settings.GetAsync(cancellationToken).ConfigureAwait(false);
        Keep(read);
        return read;
    }

    /// <summary>Opens the library to everyone, or closes it to the logged-in accounts only.</summary>
    public async Task<AccessSettings> SaveAsync(bool accountsOnly, string by, CancellationToken cancellationToken)
    {
        AccessSettings saved = new(accountsOnly, by, clock.GetUtcNow());
        await settings.SaveAsync(saved, cancellationToken).ConfigureAwait(false);
        Keep(saved);
        LogChanged(accountsOnly ? "the logged-in accounts only" : "everyone", by);
        return saved;
    }

    private void Keep(AccessSettings value)
    {
        lock (_gate)
        {
            _kept = (value, clock.GetUtcNow());
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "The library is for {Who} now (set by {By}).")]
    private partial void LogChanged(string who, string by);
}
