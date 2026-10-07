using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using TonTuyau.IntegrationTests.Support;

namespace TonTuyau.IntegrationTests.Steps;

/// <summary>A frozen clock for the backend (search periods are "the last 24 h / 7 / 30 / 365 days" from now).</summary>
[Binding]
public sealed class ClockSteps(ScenarioState state)
{
    private FakeTimeProvider? _clock;

    /// <summary>Must come before anything starts the backend (put it first in the Background).</summary>
    [Given("the clock shows {string}")]
    public void ClockShows(string instant)
    {
        _clock = new FakeTimeProvider(DateTimeOffset.Parse(instant, CultureInfo.InvariantCulture));
        state.ServiceOverrides.Add(services => services.AddSingleton<TimeProvider>(_clock));
    }

    [When("the clock moves on {int} days")]
    public void ClockMovesOn(int days) =>
        (_clock ?? throw new InvalidOperationException("No frozen clock in this scenario."))
            .Advance(TimeSpan.FromDays(days));
}
