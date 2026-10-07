using TonTuyau.Application.Store;
using TonTuyau.Infrastructure.Elasticsearch;

namespace TonTuyau.Infrastructure.UnitTests;

public sealed class IndexNamesTests
{
    [Theory, CombinatorialData]
    public void Generations_round_trip_to_their_version(
        StoreIndex index,
        [CombinatorialValues(1, 2, 17, 1000)] int version,
        [CombinatorialValues("tontuyau", "t-1a2b")] string prefix)
    {
        IndexNames names = new(prefix);

        Assert.Equal(
            version,
            names.VersionOf(
                index,
                names.Generation(index, version)));
    }

    [Theory, CombinatorialData]
    public void Aliases_are_distinct_and_prefixed(StoreIndex index)
    {
        IndexNames names = new("tontuyau");

        Assert.StartsWith("tontuyau-", names.ReadAlias(index), StringComparison.Ordinal);
        Assert.Equal(names.ReadAlias(index) + "-write", names.WriteAlias(index));
        Assert.NotEqual(
            names.ReadAlias(index),
            names.Generation(index, 1));
    }

    [Theory]
    [InlineData("tontuyau-videos")]
    [InlineData("tontuyau-videos-write")]
    [InlineData("tontuyau-videos-v")]
    [InlineData("tontuyau-videos-v0")]
    [InlineData("tontuyau-videos-v-1")]
    [InlineData("tontuyau-videos-v1x")]
    [InlineData("tontuyau-channels-v1")]
    [InlineData("other-videos-v1")]
    public void Anything_else_is_not_a_video_generation(string name)
    {
        Assert.Null(new IndexNames("tontuyau").VersionOf(StoreIndex.Videos, name));
    }
}
