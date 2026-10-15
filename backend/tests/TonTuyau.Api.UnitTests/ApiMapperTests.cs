using System.Text.Json;
using System.Text.Json.Nodes;
using TonTuyau.Api.Contracts;
using TonTuyau.Api.Hosting;
using TonTuyau.Application.Browse;
using TonTuyau.Application.Library;
using TonTuyau.Application.Paging;
using TonTuyau.Application.Store;
using TonTuyau.Application.YouTube;

namespace TonTuyau.Api.UnitTests;

public sealed class ApiMapperTests
{
    private const string VideoId = "y07hZ0XvwUg";
    private const string ChannelId = "UCmwHXyZdXAFuE6-PLQTnGOg";

    private static readonly JsonSerializerOptions Json = ApiJson();

    [Theory, CombinatorialData]
    public void Urls_are_only_given_for_images_that_are_there(bool thumbnail, bool channelThumbnail)
    {
        VideoCard card = ApiMapper.ToDto(Card(thumbnail, channelThumbnail));

        Assert.Equal(thumbnail ? $"/art/videos/{VideoId}" : null, card.ThumbnailUrl);
        Assert.Equal(channelThumbnail ? $"/art/channels/{ChannelId}/thumb" : null, card.Channel.ThumbnailUrl);
    }

    [Theory, CombinatorialData]
    public void Channel_art_urls_follow_what_was_copied(bool thumb, bool banner)
    {
        ChannelDetailView view = new(
            new ChannelCardView(
                ChannelId,
                "Tom Cooks",
                224000,
                Art(thumb),
                new TypeCounts(1, 2, 3)),
            "",
            banner ? new ArtVersions(2560, [1280]) : null,
            [],
            true);

        ChannelDetail detail = ApiMapper.ToDto(view);

        Assert.Equal(thumb ? $"/art/channels/{ChannelId}/thumb" : null, detail.ThumbnailUrl);
        Assert.Equal(banner ? $"/art/channels/{ChannelId}/banner" : null, detail.BannerUrl);
        Assert.Equal(new VideoCounts(1, 2, 3), detail.VideoCounts);
    }

    [Fact]
    public void A_video_card_is_written_as_the_contract_shows()
    {
        JsonObject json = Serialize(ApiMapper.ToDto(Card(true, true)));

        Assert.Equal(
            [
                "id",
                "title",
                "type",
                "published",
                "durationSeconds",
                "views",
                "active",
                "thumbnailUrl",
                "thumbnails",
                "channel",
            ],
            json.Select(kv => kv.Key));
        Assert.Equal("short", json["type"]!.GetValue<string>());
        Assert.Equal("2026-04-24T15:01:48Z", json["published"]!.GetValue<string>());
        Assert.Equal(1914, json["durationSeconds"]!.GetValue<long>());
        Assert.Equal(JsonValueKind.Number, json["views"]!.GetValueKind());

        // Subscribers are for the watch page only (contract v1.1): a card's channel stays as it was.
        Assert.Equal(
            ["id", "name", "thumbnailUrl", "thumbnails"],
            json["channel"]!
                .AsObject()
                .Select(kv => kv.Key));
    }

    [Fact]
    public void The_watch_page_has_the_card_fields_first_then_its_own()
    {
        VideoDetailView view = new(
            Card(true, true),
            "Description é",
            new DateTimeOffset(2026, 10, 1, 9, 13, 1, TimeSpan.FromHours(2)),
            3166,
            ["cuisine"],
            ["Howto & Style"],
            1313668754,
            "video/mp4; codecs=\"av01.0.08M.08, opus\"",
            [
                new MediaTrack(MediaTrackKind.Video, "av1", 5370710, 3840, 2160),
                new MediaTrack(MediaTrackKind.Audio, "opus", 110978, null, null),
            ],
            false,
            [new YouTubeChapter(0, 95.5, "Intro")]);

        JsonObject json = Serialize(ApiMapper.ToDto(view));

        Assert.Equal(
            [
                "id",
                "title",
                "type",
                "published",
                "durationSeconds",
                "views",
                "active",
                "thumbnailUrl",
                "thumbnails",
                "channel",
                "description",
                "downloaded",
                "likes",
                "tags",
                "categories",
                "fileSize",
                "mimeType",
                "tracks",
                "mediaUrl",
                "downloadUrl",
                "mediaAvailable",
                "chapters",
            ],
            json.Select(kv => kv.Key));
        Assert.Equal("2026-10-01T07:13:01Z", json["downloaded"]!.GetValue<string>());
        Assert.Equal(
            ["id", "name", "thumbnailUrl", "thumbnails", "subscribers"],
            json["channel"]!
                .AsObject()
                .Select(kv => kv.Key));
        Assert.Equal(224000, json["channel"]!["subscribers"]!.GetValue<long>());
        Assert.Equal(
            ["kind", "codec", "width", "height", "bitrate"],
            json["tracks"]![1]!
                .AsObject()
                .Select(kv => kv.Key));
        Assert.Null(json["tracks"]![1]!["width"]);
        Assert.Equal("audio", json["tracks"]![1]!["kind"]!.GetValue<string>());
        Assert.Equal($"/media/{VideoId}/download", json["downloadUrl"]!.GetValue<string>());
        Assert.False(json["mediaAvailable"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData(59.4, 59)]
    [InlineData(59.5, 60)]
    [InlineData(0, 0)]
    public void Durations_are_whole_seconds(double seconds, long expected)
    {
        VideoCardView card = Card(true, true) with { Duration = TimeSpan.FromSeconds(seconds) };

        Assert.Equal(expected, ApiMapper.ToDto(card).DurationSeconds);
    }

    [Theory, CombinatorialData]
    public void Search_answers_have_the_shape_of_their_kind(SearchKind kind)
    {
        ResultPage<VideoCardView> videos = new([Card(true, true)], "next", 1);
        ResultPage<ChannelCardView> channels = new([], null, 0);
        SearchResultsView results = kind switch
        {
            SearchKind.All => new(kind, [], videos, null),
            SearchKind.Videos => new(kind, null, videos, null),
            _ => new(kind, null, null, channels),
        };

        object dto = ApiMapper.ToDto(results);

        Assert.IsType(
            kind switch
            {
                SearchKind.All => typeof(SearchAllResponse),
                SearchKind.Videos => typeof(Page<VideoCard>),
                _ => typeof(Page<ChannelCard>),
            },
            dto);
    }

    [Theory, CombinatorialData]
    public void Later_pages_of_kind_all_have_an_empty_channel_list_not_a_missing_one(bool nullFromTheService)
    {
        ResultPage<VideoCardView> videos = new([Card(true, true)], null, 30);
        SearchResultsView results = new(SearchKind.All, nullFromTheService ? null : [], videos, null);

        JsonObject json = Serialize((SearchAllResponse)ApiMapper.ToDto(results));

        Assert.Equal(JsonValueKind.Array, json["channels"]!.GetValueKind());
        Assert.Empty(json["channels"]!.AsArray());
    }

    [Fact]
    public void Accents_are_written_as_they_are()
    {
        Suggestion suggestion = new("Crème brûlée", SuggestionKind.Video, VideoId);

        string json = JsonSerializer.Serialize(suggestion, Json);

        Assert.Equal($$"""{"text":"Crème brûlée","kind":"video","id":"{{VideoId}}"}""", json);
    }

    [Fact]
    public void Image_sizes_list_the_resized_versions_by_width_then_the_original_with_its_width()
    {
        VideoCard card = ApiMapper.ToDto(Card(true, true));

        Assert.Equal(
            [
                new ImageSize(360, $"/art/videos/{VideoId}/360"),
                new ImageSize(720, $"/art/videos/{VideoId}/720"),
                new ImageSize(1280, $"/art/videos/{VideoId}"),
            ],
            card.Thumbnails);
        Assert.Equal(new ImageSize(1280, $"/art/channels/{ChannelId}/thumb"), card.Channel.Thumbnails[^1]);
        Assert.Equal(new ImageSize(360, $"/art/channels/{ChannelId}/thumb/360"), card.Channel.Thumbnails[0]);
    }

    [Fact]
    public void No_image_means_no_url_and_an_empty_list()
    {
        VideoCard card = ApiMapper.ToDto(Card(false, false));

        Assert.Null(card.ThumbnailUrl);
        Assert.Empty(card.Thumbnails);
        Assert.Null(card.Channel.ThumbnailUrl);
        Assert.Empty(card.Channel.Thumbnails);
    }

    [Theory]
    [InlineData(new int[0], new[] { 900 })]
    [InlineData(new[] { 320, 48, 96 }, new[] { 48, 96, 320, 900 })]
    [InlineData(new[] { 48, 900, 1280 }, new[] { 48, 900 })]
    public void Sizes_are_sorted_with_the_original_last_and_nothing_at_or_above_it(int[] recorded, int[] expected)
    {
        IReadOnlyList<ImageSize> sizes = ApiMapper.Sizes(new ArtVersions(900, recorded), "/o", w => $"/o/{w}");

        Assert.Equal(expected, sizes.Select(s => s.Width));
        Assert.Equal("/o", sizes[^1].Url);
    }

    [Fact]
    public void A_channel_page_lists_its_banner_versions()
    {
        ChannelDetailView view = new(
            new ChannelCardView(ChannelId, "Tom Cooks", 1, null, new TypeCounts(1, 0, 0)),
            "",
            new ArtVersions(2560, [1280]),
            [],
            true);

        ChannelDetail detail = ApiMapper.ToDto(view);

        Assert.Equal(
            [
                new ImageSize(1280, $"/art/channels/{ChannelId}/banner/1280"),
                new ImageSize(2560, $"/art/channels/{ChannelId}/banner"),
            ],
            detail.Banners);
        Assert.Empty(detail.Thumbnails);
    }

    private static VideoCardView Card(bool thumbnail, bool channelThumbnail) => new(
        VideoId,
        "HUILES",
        VideoType.Short,
        new DateTimeOffset(2026, 4, 24, 17, 1, 48, TimeSpan.FromHours(2)),
        TimeSpan.FromSeconds(1914),
        74198,
        true,
        Art(thumbnail),
        new ChannelRefView(ChannelId, "Tom Cooks", Art(channelThumbnail), 224000));

    private static ArtVersions? Art(bool there) => there ? new ArtVersions(1280, [360, 720]) : null;

    private static JsonObject Serialize<T>(T value) =>
        JsonSerializer
            .SerializeToNode(value, Json)!
            .AsObject();

    private static JsonSerializerOptions ApiJson()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        ApiServices.ConfigureJson(options);
        return options;
    }
}
