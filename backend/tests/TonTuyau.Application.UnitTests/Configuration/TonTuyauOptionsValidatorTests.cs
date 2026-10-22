using Microsoft.Extensions.Configuration;
using TonTuyau.Application.Configuration;
using TonTuyau.Application.Validation;

namespace TonTuyau.Application.UnitTests.Configuration;

public sealed class TonTuyauOptionsValidatorTests
{
    private readonly TonTuyauOptionsValidator _validator = new();

    public enum RequiredField { TaUrl, TaToken, EsUrl, EsUsername, EsPassword, DbHost, DbPassword, AdminUser }

    public enum UrlField { TaUrl, EsUrl }

    public enum PathField { MediaRoot, ArtRoot }

    [Fact]
    public void Valid_options_pass()
    {
        _validator.Validate(ValidOptions.Create());
    }

    [Theory, CombinatorialData]
    public void Blank_required_field_is_reported_alone(
        RequiredField field,
        [CombinatorialValues("", "   ")] string blank)
    {
        TonTuyauOptions options = WithRequired(ValidOptions.Create(), field, blank);

        ValidationException ex = Assert.Throws<ValidationException>(() => _validator.Validate(options));

        ValidationError error = Assert.Single(ex.Errors);
        Assert.Equal(ConfigKeys.EnvironmentName(KeyOf(field)), error.Field);
    }

    [Theory, CombinatorialData]
    public void Url_must_be_absolute_http_or_https(
        UrlField field,
        [CombinatorialValues("ftp://host:21", "not a url", "/relative/path", "tubearchivist:8000")] string bad)
    {
        TonTuyauOptions options = WithRequired(
            ValidOptions.Create(),
            field == UrlField.TaUrl ? RequiredField.TaUrl : RequiredField.EsUrl,
            bad);

        ValidationException ex = Assert.Throws<ValidationException>(() => _validator.Validate(options));

        Assert.Contains("absolute http", Assert.Single(ex.Errors).Message, StringComparison.Ordinal);
    }

    [Theory, CombinatorialData]
    public void Url_accepts_http_and_https(
        UrlField field,
        [CombinatorialValues("http://192.168.1.40:7000", "https://ta.example.org")] string good)
    {
        TonTuyauOptions options = WithRequired(
            ValidOptions.Create(),
            field == UrlField.TaUrl ? RequiredField.TaUrl : RequiredField.EsUrl,
            good);

        _validator.Validate(options);
    }

    [Theory, CombinatorialData]
    public void Storage_paths_must_be_absolute(
        PathField field,
        [CombinatorialValues("", "youtube", "./data/art", "../youtube")] string bad)
    {
        TonTuyauOptions o = ValidOptions.Create();
        TonTuyauOptions options = o with
        {
            Storage = field == PathField.MediaRoot
                ? o.Storage with { MediaRoot = bad }
                : o.Storage with { ArtRoot = bad },
        };

        ValidationException ex = Assert.Throws<ValidationException>(() => _validator.Validate(options));

        Assert.Equal(
            ConfigKeys.EnvironmentName(field == PathField.MediaRoot ? ConfigKeys.MediaRoot : ConfigKeys.ArtRoot),
            Assert.Single(ex.Errors).Field);
    }

    [Theory, CombinatorialData]
    public void Index_prefix_must_be_a_valid_index_name_start(
        [CombinatorialValues(
            "",
            "Tontuyau",
            "-tontuyau",
            "_tontuyau",
            "ton tuyau",
            "ton/tuyau",
            "1tontuyau",
            "a234567890123456789012345678901234567890x")] string bad)
    {
        TonTuyauOptions o = ValidOptions.Create();

        ValidationException ex = Assert.Throws<ValidationException>(
            () => _validator.Validate(o with { Elasticsearch = o.Elasticsearch with { IndexPrefix = bad } }));

        Assert.Equal(ConfigKeys.EnvironmentName(ConfigKeys.EsIndexPrefix), Assert.Single(ex.Errors).Field);
    }

    [Theory, CombinatorialData]
    public void Index_prefix_accepts_lowercase_names(
        [CombinatorialValues("tontuyau", "t0123abcd", "ton-tuyau-2")] string good)
    {
        TonTuyauOptions o = ValidOptions.Create();

        _validator.Validate(o with { Elasticsearch = o.Elasticsearch with { IndexPrefix = good } });
    }

    [Theory, CombinatorialData]
    public void Unparsable_sync_numbers_and_flags_are_reported(
        [CombinatorialValues(ConfigKeys.TaResultWindow, ConfigKeys.ArtResizeParallelism)] string key,
        [CombinatorialValues("ten", "-5", "1.5", "yes")] string value)
    {
        IConfigurationRoot config = new ConfigurationBuilder()
            .AddInMemoryCollection(Valid().Append(new(key, value)))
            .Build();

        ValidationException ex = Assert.Throws<ValidationException>(
            () => _validator.Validate(TonTuyauOptionsReader.Read(config)));

        Assert.Equal(ConfigKeys.EnvironmentName(key), Assert.Single(ex.Errors).Field);
    }

    [Fact]
    public void Sync_settings_have_defaults()
    {
        TonTuyauOptions options = TonTuyauOptionsReader.Read(
            new ConfigurationBuilder().AddInMemoryCollection(Valid()).Build());

        _validator.Validate(options);
        Assert.Equal(
            new SyncOptions("Europe/Paris", 10_000, Environment.ProcessorCount),
            options.Sync);
    }

    [Fact]
    public void Database_and_account_settings_have_defaults()
    {
        TonTuyauOptions options = TonTuyauOptionsReader.Read(
            new ConfigurationBuilder().AddInMemoryCollection(Valid()).Build());

        _validator.Validate(options);
        Assert.Equal(new DatabaseOptions("tontuyau-db", "tontuyau", "tontuyau", "d"), options.Database);
        Assert.Equal(new AccountOptions("admin", "", true), options.Accounts);
    }

    [Theory, CombinatorialData]
    public void Resize_parallelism_must_be_between_1_and_64([CombinatorialValues(-1, 0, 65, 1000)] int parallelism)
    {
        SyncOptions sync = ValidOptions.Create().Sync with { ArtResizeParallelism = parallelism };

        ValidationException ex = Assert.Throws<ValidationException>(() =>
            _validator.Validate(ValidOptions.Create() with { Sync = sync }));

        Assert.Equal(ConfigKeys.EnvironmentName(ConfigKeys.ArtResizeParallelism), Assert.Single(ex.Errors).Field);
    }

    [Theory, CombinatorialData]
    public void Resize_parallelism_accepts_its_range([CombinatorialValues(1, 6, 64)] int parallelism)
    {
        SyncOptions sync = ValidOptions.Create().Sync with { ArtResizeParallelism = parallelism };

        _validator.Validate(ValidOptions.Create() with { Sync = sync });
    }

    [Fact]
    public void Resize_parallelism_is_read_from_its_variable()
    {
        Dictionary<string, string?> values = new(Valid()) { [ConfigKeys.ArtResizeParallelism] = "3" };

        TonTuyauOptions options = TonTuyauOptionsReader.Read(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build());

        Assert.Equal(3, options.Sync.ArtResizeParallelism);
    }

    private static IEnumerable<KeyValuePair<string, string?>> Valid() =>
    [
        new(ConfigKeys.TaUrl, "http://ta.test"),
        new(ConfigKeys.TaToken, "t"),
        new(ConfigKeys.EsUrl, "http://es.test:9200"),
        new(ConfigKeys.EsPassword, "p"),
        new(ConfigKeys.DbPassword, "d"),
    ];

    [Theory]
    [InlineData("Paris")]
    [InlineData("Europe/Atlantis")]
    [InlineData("")]
    public void Unknown_time_zone_is_rejected(string bad)
    {
        TonTuyauOptions o = ValidOptions.Create();

        ValidationException ex = Assert.Throws<ValidationException>(
            () => _validator.Validate(o with { Sync = o.Sync with { TimeZone = bad } }));

        Assert.Equal(ConfigKeys.EnvironmentName(ConfigKeys.TimeZone), Assert.Single(ex.Errors).Field);
    }

    [Fact]
    public void All_broken_rules_are_reported_at_once()
    {
        TonTuyauOptions options = new(
            new TubeArchivistOptions("", ""),
            new ElasticsearchOptions("", "", ""),
            new StorageOptions("relative", "relative"),
            new SyncOptions("x"),
            new DatabaseOptions("", "", "", ""),
            new AccountOptions("", "", true),
            new YouTubeOptions());

        ValidationException ex = Assert.Throws<ValidationException>(() => _validator.Validate(options));

        Assert.Equal(13, ex.Errors.Count);
    }

    [Fact]
    public void TubeArchivist_is_optional()
    {
        IConfigurationRoot config = new ConfigurationBuilder()
            .AddInMemoryCollection(Valid().Where(k => k.Key is not (ConfigKeys.TaUrl or ConfigKeys.TaToken)))
            .Build();

        TonTuyauOptions options = TonTuyauOptionsReader.Read(config);

        Assert.Null(options.TubeArchivist);
        _validator.Validate(options);
    }

    [Theory]
    [InlineData(ConfigKeys.TaUrl)]
    [InlineData(ConfigKeys.TaToken)]
    public void TubeArchivist_needs_both_its_address_and_its_token(string missing)
    {
        IConfigurationRoot config = new ConfigurationBuilder()
            .AddInMemoryCollection(Valid().Where(k => k.Key != missing))
            .Build();

        ValidationException ex = Assert.Throws<ValidationException>(
            () => _validator.Validate(TonTuyauOptionsReader.Read(config)));

        Assert.Equal(ConfigKeys.EnvironmentName(missing), Assert.Single(ex.Errors).Field);
    }

    [Theory, CombinatorialData]
    public void Error_message_never_contains_the_token(RequiredField brokenField)
    {
        bool isUrl = brokenField is RequiredField.TaUrl or RequiredField.EsUrl;
        TonTuyauOptions options = WithRequired(ValidOptions.Create(), brokenField, isUrl ? "%%broken%%" : "");

        ValidationException ex = Assert.Throws<ValidationException>(() => _validator.Validate(options));

        Assert.DoesNotContain(ValidOptions.Token, ex.Message, StringComparison.Ordinal);
    }

    private static string KeyOf(RequiredField field) => field switch
    {
        RequiredField.TaUrl => ConfigKeys.TaUrl,
        RequiredField.TaToken => ConfigKeys.TaToken,
        RequiredField.EsUrl => ConfigKeys.EsUrl,
        RequiredField.EsUsername => ConfigKeys.EsUsername,
        RequiredField.EsPassword => ConfigKeys.EsPassword,
        RequiredField.DbHost => ConfigKeys.DbHost,
        RequiredField.DbPassword => ConfigKeys.DbPassword,
        RequiredField.AdminUser => ConfigKeys.AdminUser,
        _ => throw new ArgumentOutOfRangeException(nameof(field)),
    };

    private static TonTuyauOptions WithRequired(TonTuyauOptions o, RequiredField field, string value) => field switch
    {
        RequiredField.TaUrl => o with { TubeArchivist = o.TubeArchivist! with { BaseUrl = value } },
        RequiredField.TaToken => o with { TubeArchivist = o.TubeArchivist! with { Token = value } },
        RequiredField.EsUrl => o with { Elasticsearch = o.Elasticsearch with { Url = value } },
        RequiredField.EsUsername => o with { Elasticsearch = o.Elasticsearch with { Username = value } },
        RequiredField.EsPassword => o with { Elasticsearch = o.Elasticsearch with { Password = value } },
        RequiredField.DbHost => o with { Database = o.Database with { Host = value } },
        RequiredField.DbPassword => o with { Database = o.Database with { Password = value } },
        RequiredField.AdminUser => o with { Accounts = o.Accounts with { AdminUser = value } },
        _ => throw new ArgumentOutOfRangeException(nameof(field)),
    };
}
