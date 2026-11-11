using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using TonTuyau.Application.Accounts;

namespace TonTuyau.Application.UnitTests.Accounts;

/// <summary>Who can watch (2.3.0): read for every request to the library, so kept a minute; a change seen at once.</summary>
public sealed class AccessServiceTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 26, 8, 0, 0, TimeSpan.Zero));
    private readonly FakeSettings _settings = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Open_to_everyone_by_default()
    {
        Assert.Equal(AccessSettings.Default, await Service().GetAsync(Ct));
    }

    [Fact]
    public async Task A_value_read_is_kept_a_minute_then_read_again()
    {
        AccessService service = Service();

        await service.GetAsync(Ct);
        _settings.Saved = new AccessSettings(AccountsOnly: true, "elsewhere", _clock.GetUtcNow());
        Assert.False((await service.GetAsync(Ct)).AccountsOnly);
        _clock.Advance(AccessService.KeptFor);
        Assert.True((await service.GetAsync(Ct)).AccountsOnly);
        Assert.Equal(2, _settings.Reads);
    }

    [Fact]
    public async Task A_change_is_saved_with_who_and_when_and_seen_at_once()
    {
        AccessService service = Service();
        await service.GetAsync(Ct);

        AccessSettings saved = await service.SaveAsync(accountsOnly: true, "admin", Ct);

        Assert.Equal(new AccessSettings(AccountsOnly: true, "admin", _clock.GetUtcNow()), saved);
        Assert.Equal(saved, _settings.Saved);
        Assert.Equal(saved, await service.GetAsync(Ct));
        Assert.Equal(1, _settings.Reads);
    }

    private AccessService Service() => new(_settings, _clock, NullLogger<AccessService>.Instance);

    private sealed class FakeSettings : IAccessSettingsRepository
    {
        public AccessSettings Saved { get; set; } = AccessSettings.Default;

        public int Reads { get; private set; }

        public Task<AccessSettings> GetAsync(CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult(Saved);
        }

        public Task SaveAsync(AccessSettings settings, CancellationToken cancellationToken)
        {
            Saved = settings;
            return Task.CompletedTask;
        }
    }
}
