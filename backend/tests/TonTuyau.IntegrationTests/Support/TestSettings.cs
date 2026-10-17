using TonTuyau.Application.Configuration;

namespace TonTuyau.IntegrationTests.Support;

/// <summary>Settings for one scenario, keyed by their environment-variable names (TONTUYAU_…).</summary>
public sealed class TestSettings
{
    public const string Token = "integration-test-token-0f9a";
    public const string AdminPassword = "integration-admin-password";

    private readonly Dictionary<string, string?> _values = new()
    {
        [ConfigKeys.EnvironmentName(ConfigKeys.TaUrl)] = "http://tubearchivist.test:8000",
        [ConfigKeys.EnvironmentName(ConfigKeys.TaToken)] = Token,
        [ConfigKeys.EnvironmentName(ConfigKeys.EsUrl)] = "http://es.test:9200",
        [ConfigKeys.EnvironmentName(ConfigKeys.EsPassword)] = "es-password",
        [ConfigKeys.EnvironmentName(ConfigKeys.SyncSchedulerEnabled)] = "false",
        // PostgresHooks points every scenario at a database of its own; the admin is created at its first start.
        [ConfigKeys.EnvironmentName(ConfigKeys.DbPassword)] = "db-password",
        [ConfigKeys.EnvironmentName(ConfigKeys.AdminPassword)] = AdminPassword,
        // The yt-dlp versions downloaded (1.5) go to a folder of the scenario's own (GitHub: see OutsideRedirect).
        [ConfigKeys.EnvironmentName(ConfigKeys.ToolsRoot)] =
            Path.Combine(Path.GetTempPath(), "tontuyau-tools-" + Guid.NewGuid().ToString("N")),
    };

    /// <summary>The scenario's own folder for the yt-dlp versions downloaded.</summary>
    public string ToolsRoot => _values[ConfigKeys.EnvironmentName(ConfigKeys.ToolsRoot)]!;

    public void Set(string environmentName, string? value) => _values[environmentName] = value;

    public string? Get(string environmentName) => _values.GetValueOrDefault(environmentName);

    /// <summary>Configuration keys as the app reads them (prefix stripped, like the env provider does).</summary>
    public IReadOnlyDictionary<string, string?> ToConfiguration() =>
        _values.ToDictionary(
            kv => kv.Key.StartsWith(ConfigKeys.EnvironmentPrefix, StringComparison.Ordinal)
                ? kv.Key[ConfigKeys.EnvironmentPrefix.Length..]
                : kv.Key,
            kv => kv.Value);
}
