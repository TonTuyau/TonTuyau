using TonTuyau.Application.Browse;
using TonTuyau.Application.Library;
using TonTuyau.Application.Store;

namespace TonTuyau.Application.UnitTests.Browse;

public sealed class FingerprintsTests
{
    private static readonly DateTimeOffset Day = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Every query of the matrix gets its own fingerprint: a cursor can't continue another list.</summary>
    [Fact]
    public void Every_filter_and_sort_changes_the_fingerprint()
    {
        string[] fingerprints = Queries()
            .Select(q => Fingerprints.Of("videos", q))
            .ToArray();

        int distinct = fingerprints
            .Distinct(StringComparer.Ordinal)
            .Count();
        Assert.Equal(fingerprints.Length, distinct);
    }

    [Theory, CombinatorialData]
    public void The_list_counts_too(
        [CombinatorialValues("videos", "channel-videos", "search-all", "search-videos")] string scope,
        [CombinatorialValues("videos", "channel-videos", "search-all", "search-videos")] string other)
    {
        VideoQuery query = new(SearchText.Parse("x"), VideoFilter.None, VideoListSort.Latest);

        bool same = Fingerprints.Of(scope, query) == Fingerprints.Of(other, query);

        Assert.Equal(scope == other, same);
    }

    [Theory, CombinatorialData]
    public void The_published_period_is_part_of_the_fingerprint(
        [CombinatorialValues(null, PublishedPeriod.Today, PublishedPeriod.Week, PublishedPeriod.Year)]
        PublishedPeriod? period,
        [CombinatorialValues(null, PublishedPeriod.Today, PublishedPeriod.Week, PublishedPeriod.Year)]
        PublishedPeriod? other)
    {
        VideoQuery query = new(SearchText.Parse("x"), VideoFilter.None, VideoListSort.Relevance);

        bool same = Fingerprints.Of("search-videos", query, period) == Fingerprints.Of("search-videos", query, other);

        Assert.Equal(period == other, same);
    }

    [Fact]
    public void Texts_that_only_differ_in_where_they_are_split_give_different_fingerprints()
    {
        ChannelQuery first = new("a|b", SearchText.Parse("c"), ChannelListSort.Name);
        ChannelQuery second = new("a", SearchText.Parse("b|c"), ChannelListSort.Name);

        Assert.NotEqual(
            Fingerprints.Of("channels", first),
            Fingerprints.Of("channels", second));
    }

    [Fact]
    public void The_same_query_always_gives_the_same_fingerprint()
    {
        VideoFilter filter = new(VideoType.Short, null, Day, DurationRange.Long);
        VideoQuery query = new(SearchText.Parse("creme"), filter, VideoListSort.Popular);

        Assert.Equal(
            Fingerprints.Of("search-videos", query),
            Fingerprints.Of("search-videos", query with { Filter = filter with { } }));
    }

    [Fact]
    public void A_word_being_typed_and_a_complete_word_give_different_fingerprints()
    {
        VideoQuery typing = new(SearchText.Parse("creme b"), VideoFilter.None, VideoListSort.Relevance);
        VideoQuery complete = typing with { Text = SearchText.Parse("creme b ") };

        Assert.NotEqual(Fingerprints.Of("search-videos", typing), Fingerprints.Of("search-videos", complete));
    }

    private static IEnumerable<VideoQuery> Queries()
    {
        foreach (string? text in (string?[])[null, "creme", "crème", "creme ", "creme b", "creme b "])
        {
            foreach (VideoType? type in (VideoType?[])[null, VideoType.Video, VideoType.Short, VideoType.Stream])
            {
                foreach (string? channel in (string?[])[null, "UCaaaaaaaaaaaaaaaaaaaaaa"])
                {
                    foreach (DateTimeOffset? from in (DateTimeOffset?[])[null, Day, Day.AddSeconds(1)])
                    {
                        foreach (DurationRange? duration in Durations)
                        {
                            foreach (VideoListSort sort in Enum.GetValues<VideoListSort>())
                            {
                                foreach (string? exclude in (string?[])[null, "abcdefghijk", "bbbbbbbbbbb"])
                                {
                                    yield return new VideoQuery(
                                        text is null ? null : SearchText.Parse(text),
                                        new VideoFilter(type, channel, from, duration, exclude),
                                        sort);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private static readonly DurationRange?[] Durations = [null, DurationRange.Short, DurationRange.Long];
}
