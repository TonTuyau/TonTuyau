using System.Globalization;
using Microsoft.Extensions.Configuration;
using TonTuyau.Application.Validation;

namespace TonTuyau.Application.Configuration;

public static class TonTuyauOptionsReader
{
    public const string DefaultEsUsername = "elastic";
    public const string DefaultIndexPrefix = "tontuyau";
    public const string DefaultMediaRoot = "/youtube";
    public const string DefaultArtRoot = "/data/art";
    public const string DefaultIncrementalCron = "0 7-23 * * *";
    public const string DefaultFullCron = "0 1 * * 6";
    public const string DefaultTimeZone = "Europe/Paris";
    public const string DefaultSyncLogRoot = "/data/logs/sync";
    public const int DefaultMaxDeletions = 100;
    public const int DefaultMarginHours = 24;
    public const int DefaultTaResultWindow = 10_000;
    public const int DefaultRejectGuardPercent = 50;
    public const int DefaultRejectGuardMinDocuments = 20;

    /// <summary>Reads the options; missing optional values get their defaults, missing required ones stay empty for the validator.</summary>
    public static TonTuyauOptions Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string Get(string key, string fallback = "") =>
            configuration[key] is { Length: > 0 } value ? value : fallback;

        List<ValidationError> unparsable = new();

        int GetInt(string key, int fallback)
        {
            string raw = Get(key);
            if (raw.Length == 0)
            {
                return fallback;
            }

            if (int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out int value))
            {
                return value;
            }

            unparsable.Add(new(ConfigKeys.EnvironmentName(key), "must be a whole number, 0 or more."));
            return fallback;
        }

        bool GetBool(string key, bool fallback)
        {
            string raw = Get(key);
            if (raw.Length == 0)
            {
                return fallback;
            }

            if (bool.TryParse(raw, out bool value))
            {
                return value;
            }

            unparsable.Add(new(ConfigKeys.EnvironmentName(key), "must be true or false."));
            return fallback;
        }

        return new TonTuyauOptions(
            new TubeArchivistOptions(Get(ConfigKeys.TaUrl), Get(ConfigKeys.TaToken)),
            new ElasticsearchOptions(
                Get(ConfigKeys.EsUrl),
                Get(ConfigKeys.EsUsername, DefaultEsUsername),
                Get(ConfigKeys.EsPassword),
                Get(ConfigKeys.EsIndexPrefix, DefaultIndexPrefix)),
            new StorageOptions(
                Get(ConfigKeys.MediaRoot, DefaultMediaRoot),
                Get(ConfigKeys.ArtRoot, DefaultArtRoot)),
            new SyncOptions(
                Get(ConfigKeys.SyncIncrementalCron, DefaultIncrementalCron),
                Get(ConfigKeys.SyncFullCron, DefaultFullCron),
                Get(ConfigKeys.TimeZone, DefaultTimeZone),
                Get(ConfigKeys.SyncLogRoot, DefaultSyncLogRoot),
                GetInt(ConfigKeys.SyncMaxDeletions, DefaultMaxDeletions),
                GetInt(ConfigKeys.SyncMarginHours, DefaultMarginHours),
                GetBool(ConfigKeys.SyncSchedulerEnabled, true),
                GetInt(ConfigKeys.TaResultWindow, DefaultTaResultWindow),
                GetInt(ConfigKeys.SyncRejectGuardPercent, DefaultRejectGuardPercent),
                GetInt(ConfigKeys.SyncRejectGuardMinDocuments, DefaultRejectGuardMinDocuments),
                GetInt(ConfigKeys.ArtResizeParallelism, Environment.ProcessorCount)))
        {
            Unparsable = unparsable,
        };
    }
}
