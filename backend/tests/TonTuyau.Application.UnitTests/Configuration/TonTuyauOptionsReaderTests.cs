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
        Assert.Equal(TonTuyauOptionsReader.DefaultTimeZone, options.Sync.TimeZone);
    }

    [Fact]
    public void Missing_required_keys_stay_empty_for_the_validator()
    {
        TonTuyauOptions options = TonTuyauOptionsReader.Read(Config(new Dictionary<string, string?>()));

        // TubeArchivist is optional (2.0): without its settings, none.
        Assert.Null(options.TubeArchivist);
        Assert.Equal("", options.Elasticsearch.Url);
        Assert.Equal("", options.Elasticsearch.Password);
    }

    [Theory, CombinatorialData]
    public void Present_value_wins_over_default_and_empty_falls_back(
        [CombinatorialValues(
            ConfigKeys.EsUsername,
            ConfigKeys.MediaRoot,
            ConfigKeys.ArtRoot,
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

    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
