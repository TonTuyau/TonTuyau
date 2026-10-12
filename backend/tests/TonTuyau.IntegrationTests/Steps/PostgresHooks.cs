using TonTuyau.Application.Configuration;
using TonTuyau.IntegrationTests.Support;

namespace TonTuyau.IntegrationTests.Steps;

/// <summary>Every backend needs its PostgreSQL (it applies its migrations at startup): a database per scenario.</summary>
[Binding]
public sealed class PostgresHooks(ScenarioState state)
{
    private string? _database;

    [BeforeScenario]
    public async Task UseRealPostgres()
    {
        _database = "t" + Guid.NewGuid().ToString("N")[..12];
        state.Settings.Set(ConfigKeys.EnvironmentName(ConfigKeys.DbHost), await PostgresContainer.GetHostAsync());
        state.Settings.Set(ConfigKeys.EnvironmentName(ConfigKeys.DbName), _database);
        state.Settings.Set(ConfigKeys.EnvironmentName(ConfigKeys.DbUsername), PostgresContainer.Username);
        state.Settings.Set(ConfigKeys.EnvironmentName(ConfigKeys.DbPassword), PostgresContainer.Password);
    }

    /// <summary>The scenario's database goes with it, once its backend stopped.</summary>
    [AfterScenario(Order = 100_000)]
    public async Task DropScenarioDatabase()
    {
        if (_database is not null)
        {
            state.StopBackend();
            await PostgresContainer.DropDatabaseAsync(_database);
        }
    }

    [AfterTestRun]
    public static Task StopPostgres() => PostgresContainer.StopAsync();
}
