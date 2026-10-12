using Cronos;
using TonTuyau.Application.Validation;

namespace TonTuyau.Application.Configuration;

/// <summary>Refuses broken configuration at startup. Messages never echo secret values.</summary>
public sealed partial class TonTuyauOptionsValidator : IValidator<TonTuyauOptions>
{
    public const int MaxArtResizeParallelism = 64;

    public void Validate(TonTuyauOptions value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidationErrors errors = new();

        RequireHttpUrl(errors, ConfigKeys.TaUrl, value.TubeArchivist.BaseUrl);
        RequireValue(errors, ConfigKeys.TaToken, value.TubeArchivist.Token);
        RequireHttpUrl(errors, ConfigKeys.EsUrl, value.Elasticsearch.Url);
        RequireValue(errors, ConfigKeys.EsUsername, value.Elasticsearch.Username);
        RequireValue(errors, ConfigKeys.EsPassword, value.Elasticsearch.Password);
        RequireIndexPrefix(errors, ConfigKeys.EsIndexPrefix, value.Elasticsearch.IndexPrefix);
        RequireAbsolutePath(errors, ConfigKeys.MediaRoot, value.Storage.MediaRoot);
        RequireAbsolutePath(errors, ConfigKeys.ArtRoot, value.Storage.ArtRoot);
        RequireCron(errors, ConfigKeys.SyncIncrementalCron, value.Sync.IncrementalCron);
        RequireCron(errors, ConfigKeys.SyncFullCron, value.Sync.FullCron);
        RequireTimeZone(errors, ConfigKeys.TimeZone, value.Sync.TimeZone);
        RequireAbsolutePath(errors, ConfigKeys.SyncLogRoot, value.Sync.LogRoot);
        if (value.Sync.TaResultWindow < 1)
        {
            errors.Add(Field(ConfigKeys.TaResultWindow), "must be 1 or more.");
        }

        if (value.Sync.RejectGuardPercent is < 0 or > 100)
        {
            errors.Add(Field(ConfigKeys.SyncRejectGuardPercent), "must be between 0 and 100.");
        }

        if (value.Sync.ArtResizeParallelism is < 1 or > MaxArtResizeParallelism)
        {
            errors.Add(Field(ConfigKeys.ArtResizeParallelism), $"must be between 1 and {MaxArtResizeParallelism}.");
        }

        RequireValue(errors, ConfigKeys.DbHost, value.Database.Host);
        RequireValue(errors, ConfigKeys.DbName, value.Database.Name);
        RequireValue(errors, ConfigKeys.DbUsername, value.Database.Username);
        RequireValue(errors, ConfigKeys.DbPassword, value.Database.Password);
        // The admin's password is only needed to create the account, so only the startup's first run checks it.
        RequireValue(errors, ConfigKeys.AdminUser, value.Accounts.AdminUser);
        if (!value.Accounts.TrustedProxies
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(part => System.Net.IPAddress.TryParse(part, out _)))
        {
            errors.Add(
                Field(ConfigKeys.TrustedProxies),
                "must be IP addresses separated by commas (your reverse proxy's, as the front server sees it).");
        }

        foreach (ValidationError error in value.Unparsable)
        {
            errors.Add(error.Field, error.Message);
        }

        errors.ThrowIfAny("configuration");
    }

    private static string Field(string key) => ConfigKeys.EnvironmentName(key);

    private static void RequireValue(ValidationErrors errors, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(Field(key), "is required.");
        }
    }

    private static void RequireHttpUrl(ValidationErrors errors, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(Field(key), "is required.");
        }
        else if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            errors.Add(Field(key), "must be an absolute http:// or https:// URL.");
        }
    }

    private static void RequireIndexPrefix(ValidationErrors errors, string key, string value)
    {
        // Elasticsearch index names: lowercase, no leading '-', '_' or '+', limited charset.
        if (!IndexPrefixFormat().IsMatch(value))
        {
            errors.Add(Field(key), "must be 1-40 lowercase letters, digits or '-', starting with a letter.");
        }
    }

    private static void RequireAbsolutePath(ValidationErrors errors, string key, string value)
    {
        if (!Path.IsPathFullyQualified(value))
        {
            errors.Add(Field(key), "must be an absolute path.");
        }
    }

    private static void RequireCron(ValidationErrors errors, string key, string value)
    {
        try
        {
            CronExpression.Parse(value, CronFormat.Standard);
        }
        catch (CronFormatException)
        {
            errors.Add(Field(key), "must be a 5-field cron expression (minute hour day month weekday).");
        }
    }

    private static void RequireTimeZone(ValidationErrors errors, string key, string value)
    {
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(value, out _))
        {
            errors.Add(Field(key), "must be an IANA time zone id, e.g. Europe/Paris.");
        }
    }

    [System.Text.RegularExpressions.GeneratedRegex("^[a-z][a-z0-9-]{0,39}\\z")]
    private static partial System.Text.RegularExpressions.Regex IndexPrefixFormat();
}
