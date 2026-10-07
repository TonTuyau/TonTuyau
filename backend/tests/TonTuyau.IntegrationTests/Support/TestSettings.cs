using TonTuyau.Application.Configuration;

namespace TonTuyau.IntegrationTests.Support;

/// <summary>Settings for one scenario, keyed by their environment-variable names (TONTUYAU_…).</summary>
public sealed class TestSettings
{
    public const string Token = "integration-test-token-0f9a";

    private readonly Dictionary<string, string?> _values = new()
    {
        [ConfigKeys.EnvironmentName(ConfigKeys.TaUrl)] = "http://tubearchivist.test:8000",
        [ConfigKeys.EnvironmentName(ConfigKeys.TaToken)] = Token,
        [ConfigKeys.EnvironmentName(ConfigKeys.EsUrl)] = "http://es.test:9200",
        [ConfigKeys.EnvironmentName(ConfigKeys.EsPassword)] = "es-password",
        [ConfigKeys.EnvironmentName(ConfigKeys.SyncSchedulerEnabled)] = "false",
    };

    public void Set(string environmentName, string? value) => _values[environmentName] = value;

    /// <summary>Configuration keys as the app reads them (prefix stripped, like the env provider does).</summary>
    public IReadOnlyDictionary<string, string?> ToConfiguration() =>
        _values.ToDictionary(
            kv => kv.Key.StartsWith(ConfigKeys.EnvironmentPrefix, StringComparison.Ordinal)
                ? kv.Key[ConfigKeys.EnvironmentPrefix.Length..]
                : kv.Key,
            kv => kv.Value);
}
