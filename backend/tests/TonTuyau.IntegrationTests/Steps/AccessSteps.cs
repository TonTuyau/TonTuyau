using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.Accounts;
using TonTuyau.IntegrationTests.Support;

namespace TonTuyau.IntegrationTests.Steps;

/// <summary>Who can watch (2.3.0), set as the admin's accounts page sets it.</summary>
[Binding]
public sealed class AccessSteps(ScenarioState state)
{
    [Given("the library is for its accounts only")]
    public async Task AccountsOnly()
    {
        await state.WaitUntilReadyAsync();
        await state.Backend.Services
            .GetRequiredService<AccessService>()
            .SaveAsync(accountsOnly: true, "test", TestContext.Current.CancellationToken);
    }
}
