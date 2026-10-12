namespace TonTuyau.Application.Configuration;

/// <summary>
/// Flat configuration keys. As environment variables they carry the <see cref="EnvironmentPrefix"/>,
/// e.g. <c>TONTUYAU_TA_URL</c>.
/// </summary>
public static class ConfigKeys
{
    public const string EnvironmentPrefix = "TONTUYAU_";

    public const string TaUrl = "TA_URL";
    public const string TaToken = "TA_TOKEN";
    public const string EsUrl = "ES_URL";
    public const string EsUsername = "ES_USERNAME";
    public const string EsPassword = "ES_PASSWORD";
    public const string EsIndexPrefix = "ES_INDEX_PREFIX";
    public const string MediaRoot = "MEDIA_ROOT";
    public const string ArtRoot = "ART_ROOT";
    public const string SyncIncrementalCron = "SYNC_INCREMENTAL_CRON";
    public const string SyncFullCron = "SYNC_FULL_CRON";
    public const string TimeZone = "TZ";
    public const string SyncLogRoot = "SYNC_LOG_ROOT";
    public const string SyncMaxDeletions = "SYNC_MAX_DELETIONS";
    public const string SyncMarginHours = "SYNC_INCREMENTAL_MARGIN_HOURS";
    public const string SyncSchedulerEnabled = "SYNC_SCHEDULER_ENABLED";
    public const string TaResultWindow = "TA_RESULT_WINDOW";
    public const string SyncRejectGuardPercent = "SYNC_REJECT_GUARD_PERCENT";
    public const string SyncRejectGuardMinDocuments = "SYNC_REJECT_GUARD_MIN_DOCUMENTS";
    public const string ArtResizeParallelism = "ART_RESIZE_PARALLELISM";
    public const string DbHost = "DB_HOST";
    public const string DbName = "DB_NAME";
    public const string DbUsername = "DB_USERNAME";
    public const string DbPassword = "DB_PASSWORD";
    public const string AdminUser = "ADMIN_USER";
    public const string AdminPassword = "ADMIN_PASSWORD";
    public const string CookieSecure = "COOKIE_SECURE";

    public static string EnvironmentName(string key) => EnvironmentPrefix + key;
}
