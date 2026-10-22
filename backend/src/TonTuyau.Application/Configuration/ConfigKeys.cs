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
    public const string TimeZone = "TZ";
    public const string TaResultWindow = "TA_RESULT_WINDOW";
    public const string ArtResizeParallelism = "ART_RESIZE_PARALLELISM";
    public const string DbHost = "DB_HOST";
    public const string DbName = "DB_NAME";
    public const string DbUsername = "DB_USERNAME";
    public const string DbPassword = "DB_PASSWORD";
    public const string AdminUser = "ADMIN_USER";
    public const string AdminPassword = "ADMIN_PASSWORD";
    public const string CookieSecure = "COOKIE_SECURE";
    public const string TrustedProxies = "TRUSTED_PROXIES";

    /// <summary>The Unix socket the command line reaches the backend through (1.4); empty: none.</summary>
    public const string CliSocket = "CLI_SOCKET";

    public const string YtdlpPath = "YTDLP_PATH";
    public const string DenoPath = "DENO_PATH";
    public const string ToolsRoot = "TOOLS_ROOT";
    public const string FfmpegPath = "FFMPEG_PATH";

    public static string EnvironmentName(string key) => EnvironmentPrefix + key;
}
