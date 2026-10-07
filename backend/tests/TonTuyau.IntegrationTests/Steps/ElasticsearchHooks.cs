using TonTuyau.Application.Configuration;
using TonTuyau.IntegrationTests.Support;

namespace TonTuyau.IntegrationTests.Steps;

[Binding]
public sealed class ElasticsearchHooks(ScenarioState state)
{
    private string? _prefix;

    /// <summary>Real Elasticsearch, with a prefix of its own so scenarios never see each other's indices.</summary>
    [BeforeScenario("elasticsearch")]
    public async Task UseRealElasticsearch()
    {
        state.Settings.Set(ConfigKeys.EnvironmentName(ConfigKeys.EsUrl), await ElasticsearchContainer.GetUrlAsync());
        state.Settings.Set(ConfigKeys.EnvironmentName(ConfigKeys.EsPassword), ElasticsearchContainer.Password);
        _prefix = "t" + Guid.NewGuid().ToString("N")[..12];
        state.Settings.Set(ConfigKeys.EnvironmentName(ConfigKeys.EsIndexPrefix), _prefix);
    }

    /// <summary>The scenario's indices go with it, once its backend (and anything it runs in the background) stopped.</summary>
    [AfterScenario("elasticsearch", Order = 100_000)]
    public async Task DeleteScenarioIndices()
    {
        if (_prefix is not null)
        {
            state.StopBackend();
            await ElasticsearchContainer.DeleteIndicesAsync(_prefix);
        }
    }

    [AfterTestRun]
    public static Task StopElasticsearch() => ElasticsearchContainer.StopAsync();

    /// <summary>
    /// Dozens of hosts start in one test run; each would watch its config files for changes (inotify) and Linux
    /// allows 128 watchers per user. Reloading config is useless in tests.
    /// </summary>
    [BeforeTestRun]
    public static void NoConfigReload() =>
        Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false");
}
