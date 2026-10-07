using Microsoft.Extensions.Configuration;
using TonTuyau.Application.Configuration;

namespace TonTuyau.Application.UnitTests.Configuration;

public sealed class TonTuyauOptionsReaderTests
{
    [Fact]
    public void Missing_optional_keys_get_defaults()
    {
        TonTuyauOptions options = TonTuyauOptionsReader.Read(Config(new Dictionary<string, string?>()));

        Assert.Equal(TonTuyauOptionsReader.DefaultEsUsername, options.Elasticsearch.Username);
        Assert.Equal(TonTuyauOptionsReader.DefaultIndexPrefix, options.Elasticsearch.IndexPrefix);
        Assert.Equal(TonTuyauOptionsReader.DefaultMediaRoot, options.Storage.MediaRoot);
        Assert.Equal(TonTuyauOptionsReader.DefaultArtRoot, options.Storage.ArtRoot);
        Assert.Equal(TonTuyauOptionsReader.DefaultIncrementalCron, options.Sync.IncrementalCron);
        Assert.Equal(TonTuyauOptionsReader.DefaultFullCron, options.Sync.FullCron);
        Assert.Equal(TonTuyauOptionsReader.DefaultTimeZone, options.Sync.TimeZone);
    }

    [Fact]
    public void Missing_required_keys_stay_empty_for_the_validator()
    {
        TonTuyauOptions options = TonTuyauOptionsReader.Read(Config(new Dictionary<string, string?>()));

        Assert.Equal("", options.TubeArchivist.BaseUrl);
        Assert.Equal("", options.TubeArchivist.Token);
        Assert.Equal("", options.Elasticsearch.Url);
        Assert.Equal("", options.Elasticsearch.Password);
    }

    [Theory, CombinatorialData]
    public void Present_value_wins_over_default_and_empty_falls_back(
        [CombinatorialValues(
            ConfigKeys.EsUsername,
            ConfigKeys.MediaRoot,
            ConfigKeys.ArtRoot,
            ConfigKeys.SyncIncrementalCron,
            ConfigKeys.SyncFullCron,
            ConfigKeys.TimeZone)] string key,
        bool empty)
    {
        TonTuyauOptions options = TonTuyauOptionsReader.Read(
            Config(new Dictionary<string, string?> { [key] = empty ? "" : "custom" }));

        string actual = key switch
        {
            ConfigKeys.EsUsername => options.Elasticsearch.Username,
            ConfigKeys.MediaRoot => options.Storage.MediaRoot,
            ConfigKeys.ArtRoot => options.Storage.ArtRoot,
            ConfigKeys.SyncIncrementalCron => options.Sync.IncrementalCron,
            ConfigKeys.SyncFullCron => options.Sync.FullCron,
            _ => options.Sync.TimeZone,
        };
        if (empty)
        {
            Assert.NotEqual("custom", actual);
            Assert.NotEqual("", actual);
        }
        else
        {
            Assert.Equal("custom", actual);
        }
    }

    [Fact]
    public void Default_schedule_is_hourly_daytime_incremental_and_full_on_saturday_one_am()
    {
        Assert.Equal("0 7-23 * * *", TonTuyauOptionsReader.DefaultIncrementalCron);
        Assert.Equal("0 1 * * 6", TonTuyauOptionsReader.DefaultFullCron);
    }

    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
