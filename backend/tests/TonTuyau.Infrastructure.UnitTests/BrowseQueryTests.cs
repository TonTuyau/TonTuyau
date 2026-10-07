using System.Text.Json.Nodes;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Elastic.Transport;
using TonTuyau.Application.Configuration;
using TonTuyau.Application.Library;
using TonTuyau.Application.Paging;
using TonTuyau.Application.Store;
using TonTuyau.Application.Validation;
using TonTuyau.Infrastructure.Elasticsearch;

namespace TonTuyau.Infrastructure.UnitTests;

/// <summary>The typed queries behind the API, as Elasticsearch receives them (no Elasticsearch needed).</summary>
public sealed class BrowseQueryTests
{
    private static readonly ElasticsearchClient Client =
        EsClient.Create(new ElasticsearchOptions("http://es.test:9200", "elastic", "password"));

    private static readonly DateTimeOffset Day = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

    [Theory, CombinatorialData]
    public void Video_queries_filter_and_search_as_asked(
        [CombinatorialValues(null, "creme brulee")] string? text,
        [CombinatorialValues(null, VideoType.Video, VideoType.Short, VideoType.Stream)] VideoType? type,
        [CombinatorialValues(null, "UCaaaaaaaaaaaaaaaaaaaaaa")] string? channel,
        [CombinatorialValues(null, DurationRange.Short, DurationRange.Medium, DurationRange.Long)]
        DurationRange? duration,
        bool dated)
    {
        VideoFilter filter = new(type, channel, dated ? Day : null, duration);

        VideoQuery videoQuery = new(text is null ? null : SearchText.Parse(text + " "), filter, VideoListSort.Latest);

        JsonNode query = Json(VideoRepository.QueryFor(videoQuery))["bool"]!;

        JsonNode[] filters = Many(query["filter"]);
        int given = new object?[] { type, channel, dated ? Day : null, duration }.Count(f => f is not null);
        Assert.Equal(given, filters.Length);
        if (type is { } t)
        {
            string name = t
                .ToString()
                .ToLowerInvariant();
            Assert.Contains(filters, f => f["term"]?["type"]?["value"]?.GetValue<string>() == name);
        }

        if (channel is not null)
        {
            Assert.Contains(filters, f => f["term"]?["channel_id"]?["value"]?.GetValue<string>() == channel);
        }

        if (dated)
        {
            JsonNode published = filters.Single(f => f["range"]?["published"] is not null)["range"]!["published"]!;
            Assert.NotNull(published["gte"]);
            Assert.Null(published["lt"]);
        }

        if (duration is { } d)
        {
            JsonNode range = filters
                .Single(f => f["range"]?["duration_seconds"] is not null)["range"]!["duration_seconds"]!;
            Assert.Equal(ExpectedDuration(d), Bounds(range));
        }

        Assert.Equal(text is not null, query["must"] is not null);
        if (text is not null)
        {
            JsonNode match = Many(query["must"])[0]["multi_match"]!;
            Assert.Equal(text, match["query"]!.GetValue<string>());
            Assert.Equal("cross_fields", match["type"]!.GetValue<string>());
            Assert.Equal("and", match["operator"]!.GetValue<string>());
            Assert.Equal(
                [
                    "title^3",
                    "title.en^3",
                    "channel_name^2",
                    "channel_name.en^2",
                    "tags^1.5",
                    "description",
                    "description.en",
                ],
                match["fields"]!
                    .AsArray()
                    .Select(f => f!.GetValue<string>()));
        }
    }

    [Fact]
    public void The_last_word_being_typed_matches_whole_or_as_the_start_of_a_word_in_any_field()
    {
        VideoQuery videoQuery = new(SearchText.Parse("creme b"), VideoFilter.None, VideoListSort.Relevance);

        JsonNode query = Json(VideoRepository.QueryFor(videoQuery))["bool"]!;

        JsonNode[] must = Many(query["must"]);
        Assert.Equal(2, must.Length);
        Assert.Equal("creme", must[0]["multi_match"]!["query"]!.GetValue<string>());
        JsonNode last = must[1]["bool"]!;
        Assert.Equal("1", last["minimum_should_match"]!.ToString());
        JsonNode[] should = Many(last["should"]);
        Assert.Equal("b", should[0]["multi_match"]!["query"]!.GetValue<string>());
        Assert.Equal("and", should[0]["multi_match"]!["operator"]!.GetValue<string>());
        JsonNode title = should[1]["match"]!["title.autocomplete"]!;
        JsonNode channel = should[2]["match"]!["channel_name.autocomplete"]!;
        Assert.Equal("b", title["query"]!.GetValue<string>());
        Assert.Equal("b", channel["query"]!.GetValue<string>());
        // Title words weigh more than channel words, and both less than a whole word (boost 1 × the field's weight).
        Assert.Equal(1.5f, title["boost"]!.GetValue<float>());
        Assert.Equal(1f, channel["boost"]!.GetValue<float>());

        // Tags and descriptions (QA11 M5-13): indexed prefixes, weighed as for whole words (tags 1.5, description 1).
        JsonNode tags = should[3]["match_bool_prefix"]!["tags"]!;
        JsonNode description = should[4]["match_bool_prefix"]!["description.prefix"]!;
        Assert.Equal("b", tags["query"]!.GetValue<string>());
        Assert.Equal("and", tags["operator"]!.GetValue<string>());
        Assert.Equal(0.75f, tags["boost"]!.GetValue<float>());
        Assert.Equal("b", description["query"]!.GetValue<string>());
        Assert.Equal(0.5f, description["boost"]!.GetValue<float>());
        Assert.Equal(5, should.Length);
        Assert.Equal("creme b", Many(query["should"])[0]["match_phrase"]!["title"]!["query"]!.GetValue<string>());
    }

    [Fact]
    public void A_text_ending_with_a_space_has_whole_words_only()
    {
        VideoQuery videoQuery = new(SearchText.Parse("creme b "), VideoFilter.None, VideoListSort.Relevance);

        JsonNode[] must = Many(Json(VideoRepository.QueryFor(videoQuery))["bool"]!["must"]);

        Assert.Equal("creme b", Assert.Single(must)["multi_match"]!["query"]!.GetValue<string>());
    }

    [Fact]
    public void A_single_word_being_typed_has_no_whole_words_clause()
    {
        VideoQuery videoQuery = new(SearchText.Parse("cre"), VideoFilter.None, VideoListSort.Relevance);

        JsonNode[] must = Many(Json(VideoRepository.QueryFor(videoQuery))["bool"]!["must"]);

        Assert.NotNull(Assert.Single(must)["bool"]);
    }

    [Fact]
    public void A_text_with_nothing_searchable_matches_nothing()
    {
        VideoQuery videoQuery = new(SearchText.Parse(" - "), VideoFilter.None, VideoListSort.Relevance);
        ChannelQuery channelQuery = new(null, SearchText.Parse("!"), ChannelListSort.Relevance);

        Assert.NotNull(Many(Json(VideoRepository.QueryFor(videoQuery))["bool"]!["must"])[0]["match_none"]);
        Assert.NotNull(Many(Json(ChannelRepository.QueryFor(channelQuery))["bool"]!["must"])[0]["match_none"]);
    }

    [Fact]
    public void Channel_searches_take_the_last_word_as_the_start_of_a_name_word()
    {
        ChannelQuery channelQuery = new(null, SearchText.Parse("tom c"), ChannelListSort.Relevance);

        JsonNode[] must = Many(Json(ChannelRepository.QueryFor(channelQuery))["bool"]!["must"]);

        JsonNode[] should = Many(must[1]["bool"]!["should"]);
        Assert.Equal("c", should[1]["match"]!["name.autocomplete"]!["query"]!.GetValue<string>());
        Assert.Equal("c", should[2]["match_bool_prefix"]!["tags"]!["query"]!.GetValue<string>());
        Assert.Equal("c", should[3]["match_bool_prefix"]!["description.prefix"]!["query"]!.GetValue<string>());
    }

    [Theory, CombinatorialData]
    public void An_excluded_video_is_left_out_by_id(
        [CombinatorialValues(null, "abcdefghijk")] string? exclude,
        [CombinatorialValues(null, "creme")] string? text)
    {
        VideoFilter filter = new(VideoType.Video, "UCaaaaaaaaaaaaaaaaaaaaaa", ExcludeId: exclude);
        VideoQuery videoQuery = new(text is null ? null : SearchText.Parse(text), filter, VideoListSort.Latest);

        JsonNode query = Json(VideoRepository.QueryFor(videoQuery))["bool"]!;

        JsonNode[] mustNot = Many(query["must_not"]);
        Assert.Equal(exclude is null ? 0 : 1, mustNot.Length);
        if (exclude is not null)
        {
            Assert.Equal(exclude, mustNot[0]["term"]!["id"]!["value"]!.GetValue<string>());
        }

        // Left out, not filtered by type or channel any differently.
        Assert.Equal(2, Many(query["filter"]).Length);
    }

    [Theory, CombinatorialData]
    public void Every_video_sort_ends_with_the_id(VideoListSort sort)
    {
        string[] keys = Keys(VideoRepository.SortFor(sort));

        Assert.Equal(
            sort switch
            {
                VideoListSort.Latest => ["published:desc", "id:asc"],
                VideoListSort.Popular => ["views:desc", "id:asc"],
                VideoListSort.Oldest => ["published:asc", "id:asc"],
                _ => ["_score:desc", "id:asc"],
            },
            keys);
    }

    [Theory, CombinatorialData]
    public void Every_channel_sort_ends_with_the_id(ChannelListSort sort)
    {
        string[] keys = Keys(ChannelRepository.SortFor(sort));

        Assert.Equal(
            sort switch
            {
                ChannelListSort.Name => ["name.sort:asc", "id:asc"],
                ChannelListSort.Subscribers => ["subscribers:desc", "id:asc"],
                _ => ["_score:desc", "id:asc"],
            },
            keys);
    }

    [Theory, CombinatorialData]
    public void Channel_queries_filter_by_name_prefix_and_search_text(
        [CombinatorialValues(null, "tom c")] string? prefix,
        [CombinatorialValues(null, "cuisine")] string? text)
    {
        ChannelQuery channelQuery = new(prefix, text is null ? null : SearchText.Parse(text), ChannelListSort.Name);

        JsonNode query = Json(ChannelRepository.QueryFor(channelQuery))["bool"]!;

        Assert.Equal(prefix is not null, query["filter"] is not null);
        Assert.Equal(text is not null, query["must"] is not null);
        if (prefix is not null)
        {
            JsonNode prefixQuery = Many(query["filter"])[0]["bool"]!;
            JsonNode match = Many(prefixQuery["must"])[0]["match"]!["name.autocomplete"]!;
            JsonNode phrase = Many(prefixQuery["should"])[0]["match_phrase"]!["name.autocomplete"]!;
            Assert.Equal(prefix, match["query"]!.GetValue<string>());
            Assert.Equal("and", match["operator"]!.GetValue<string>());
            Assert.Equal(prefix, phrase["query"]!.GetValue<string>());
        }
    }

    [Theory, CombinatorialData]
    public void Positions_fit_their_sort_or_are_refused(
        [CombinatorialValues("text", "integer", "real")] string value,
        [CombinatorialValues("text", "integer", "score")] string key)
    {
        SortKeyKind kind = key switch
        {
            "text" => SortKeyKind.Text,
            "integer" => SortKeyKind.Integer,
            _ => SortKeyKind.Score,
        };
        SortValue sortValue = value switch
        {
            "text" => new TextSortValue("x"),
            "integer" => new IntegerSortValue(42),
            _ => new RealSortValue(1.5),
        };
        IReadOnlyList<SortKey> keys = [Key(kind), SortKey.Id<Elasticsearch.Documents.VideoDocument>()];
        SortPosition position = new([sortValue, new TextSortValue("abcdefghijk")]);
        bool fits = (value, kind) is ("text", SortKeyKind.Text)
            or ("integer", SortKeyKind.Integer or SortKeyKind.Score)
            or ("real", SortKeyKind.Score);

        if (fits)
        {
            ICollection<FieldValue> after = SortPositions.ToSearchAfter(position, keys);
            Assert.Equal(2, after.Count);
        }
        else
        {
            Assert.Throws<ValidationException>(() => SortPositions.ToSearchAfter(position, keys));
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void A_position_of_another_length_is_refused(int length)
    {
        SortPosition position = new([.. Enumerable.Repeat<SortValue>(new TextSortValue("x"), length)]);

        IReadOnlyList<SortKey> keys = VideoRepository.SortFor(VideoListSort.Relevance);

        Assert.Throws<ValidationException>(() => SortPositions.ToSearchAfter(position, keys));
    }

    [Fact]
    public void Sort_values_from_Elasticsearch_become_positions()
    {
        SortPosition position = SortPositions.From(
        [
            FieldValue.Double(2.5),
            FieldValue.Long(7),
            FieldValue.String("id"),
        ]);

        Assert.Equal([new RealSortValue(2.5), new IntegerSortValue(7), new TextSortValue("id")], position.Values);
    }

    private static SortKey Key(SortKeyKind kind) => kind switch
    {
        SortKeyKind.Score => SortKey.Score(),
        SortKeyKind.Integer => SortKey.By<Elasticsearch.Documents.VideoDocument>(v => v.Views, SortOrder.Desc, kind),
        _ => SortKey.Id<Elasticsearch.Documents.VideoDocument>(),
    };

    private static (double?, double?, double?, double?) ExpectedDuration(DurationRange duration) => duration switch
    {
        DurationRange.Short => (null, null, null, 240),
        DurationRange.Medium => (240, null, 1200, null),
        _ => (null, 1200, null, null),
    };

    /// <returns>gte, gt, lte, lt.</returns>
    private static (double?, double?, double?, double?) Bounds(JsonNode range) => (
        range["gte"]?.GetValue<double>(),
        range["gt"]?.GetValue<double>(),
        range["lte"]?.GetValue<double>(),
        range["lt"]?.GetValue<double>());

    private static string[] Keys(IReadOnlyList<SortKey> keys) =>
        keys
            .Select(k => Json(k.Options)
                .AsObject()
                .Single())
            .Select(kv => kv.Key == "_score"
                ? $"_score:{kv.Value!["order"]!.GetValue<string>()}"
                : $"{kv.Key}:{kv.Value!["order"]!.GetValue<string>()}")
            .ToArray();

    /// <summary>The client writes a one-item list as the item itself.</summary>
    private static JsonNode[] Many(JsonNode? node) => node switch
    {
        null => [],
        JsonArray array => [.. array.OfType<JsonNode>()],
        _ => [node],
    };

    private static JsonNode Json(object value)
    {
        using MemoryStream stream = new();
        Client.RequestResponseSerializer.Serialize(value, stream);
        stream.Position = 0;
        return JsonNode.Parse(stream)!;
    }
}
