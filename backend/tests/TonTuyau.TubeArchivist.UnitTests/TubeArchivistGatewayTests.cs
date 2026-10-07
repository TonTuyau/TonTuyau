using System.Net;
using System.Text.Json;
using Polly.CircuitBreaker;
using TonTuyau.Application.Library;
using TonTuyau.Application.TubeArchivist;
using TonTuyau.Application.Validation;
using TonTuyau.TubeArchivist.UnitTests.Support;

namespace TonTuyau.TubeArchivist.UnitTests;

public sealed class TubeArchivistGatewayTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // --- Fixture-backed: the shapes and quirks of a real TA v0.5.12 ---

    [Fact]
    public async Task Ping_returns_the_version()
    {
        TubeArchivistInfo info = await GatewayFactory.Create(new FixtureHandler()).PingAsync(Ct);

        Assert.Equal("v0.5.12", info.Version);
    }

    [Fact]
    public async Task Missing_token_is_an_auth_failure()
    {
        TubeArchivistGateway gateway = GatewayFactory.Create(new FixtureHandler(), withToken: false);

        TubeArchivistAuthException ex = await Assert.ThrowsAsync<TubeArchivistAuthException>(
            () => gateway.PingAsync(Ct));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
    }

    [Fact]
    public async Task Library_stats_give_true_totals()
    {
        LibraryStats stats = await GatewayFactory.Create(new FixtureHandler()).GetLibraryStatsAsync(Ct);

        Assert.True(stats.Total > 10_000, "the stats endpoint isn't capped like the lists");
        Assert.Equal(stats.Total, stats.Videos + stats.Shorts + stats.Streams);
        Assert.Equal(stats.Total, stats.Active + stats.Inactive);
    }

    [Fact]
    public async Task Channel_count_is_the_true_total()
    {
        long count = await GatewayFactory.Create(new FixtureHandler()).GetChannelCountAsync(Ct);

        Assert.Equal(FixtureHandler.Get("stats-channel").Json.GetProperty("doc_count").GetInt64(), count);
        Assert.True(count > 100);
    }

    [Fact]
    public async Task A_channel_video_total_comes_from_its_aggregations()
    {
        string channelId = FixtureHandler.Get("channel-small").Json.GetProperty("channel_id").GetString()!;

        long? total = await GatewayFactory.Create(new FixtureHandler()).GetChannelVideoTotalAsync(channelId, Ct);

        Assert.Equal(3, total);
    }

    [Fact]
    public async Task An_unknown_channel_has_a_video_total_of_zero_not_a_404()
    {
        long? total = await GatewayFactory.Create(new FixtureHandler())
            .GetChannelVideoTotalAsync("UCaaaaaaaaaaaaaaaaaaaaaa", Ct);

        Assert.Equal(0, total);
    }

    [Fact]
    public async Task Channel_pages_end_with_an_empty_page()
    {
        TubeArchivistGateway gateway = GatewayFactory.Create(new FixtureHandler());

        Page<Channel> first = await gateway.GetChannelPageAsync(1, Ct);
        Page<Channel> second = await gateway.GetChannelPageAsync(2, Ct);
        Page<Channel> third = await gateway.GetChannelPageAsync(3, Ct);

        Assert.Equal(100, first.PageSize);
        Assert.Equal(100, first.Items.Count);
        Assert.InRange(second.Items.Count, 1, 99);
        Assert.True(third.IsPastEnd);
        Assert.Empty(first.Rejected);
        Assert.Empty(second.Rejected);
        Assert.Empty(Ids(first).Intersect(Ids(second)));
    }

    [Fact]
    public async Task Recorded_page_0_is_the_same_as_page_1_which_is_why_the_client_refuses_it()
    {
        JsonElement page0 = FixtureHandler.Get("channels-page-0").Json.GetProperty("data");
        JsonElement page1 = FixtureHandler.Get("channels-page-1").Json.GetProperty("data");
        Assert.Equal(page1.GetRawText(), page0.GetRawText());

        FixtureHandler fake = new();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => GatewayFactory.Create(fake).GetChannelPageAsync(0, Ct));
        Assert.Empty(fake.Requests);
    }

    [Theory, CombinatorialData]
    public async Task Page_numbers_below_one_are_refused_without_calling_TA(
        [CombinatorialValues(0, -1, int.MinValue)] int page,
        bool videos)
    {
        FixtureHandler fake = new();
        TubeArchivistGateway gateway = GatewayFactory.Create(fake);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => videos
            ? gateway.GetVideoPageAsync(
                new VideoPageQuery(VideoSort.Downloaded, SortOrder.Desc),
                page,
                Ct)
            : gateway.GetChannelPageAsync(page, Ct));
        Assert.Empty(fake.Requests);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(100)]
    public async Task Every_recorded_video_of_a_full_page_passes_validation(int page)
    {
        Page<Video> result = await GatewayFactory.Create(new FixtureHandler())
            .GetVideoPageAsync(
                new VideoPageQuery(VideoSort.Downloaded, SortOrder.Desc),
                page,
                Ct);

        Assert.Empty(result.Rejected);
        Assert.Equal(100, result.Items.Count);
        Assert.True(result.TotalIsCapped);
        Assert.Equal(10_000, result.TotalHits);
    }

    [Fact]
    public async Task Pages_are_one_based_and_do_not_overlap()
    {
        TubeArchivistGateway gateway = GatewayFactory.Create(new FixtureHandler());
        VideoPageQuery query = new(VideoSort.Downloaded, SortOrder.Desc);

        Page<Video> first = await gateway.GetVideoPageAsync(query, 1, Ct);
        Page<Video> second = await gateway.GetVideoPageAsync(query, 2, Ct);

        Assert.Empty(Ids(first).Intersect(Ids(second)));
        Assert.True(first.Items[^1].Downloaded >= second.Items[0].Downloaded);
    }

    [Fact]
    public async Task Past_the_10k_window_TA_fails_with_a_500()
    {
        TubeArchivistGateway gateway = GatewayFactory.Create(new FixtureHandler());

        TubeArchivistServerException ex = await Assert.ThrowsAsync<TubeArchivistServerException>(() =>
            gateway.GetVideoPageAsync(
                new VideoPageQuery(VideoSort.Downloaded, SortOrder.Desc),
                101,
                Ct));

        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
    }

    [Fact]
    public async Task A_small_channel_ends_on_its_second_page()
    {
        TubeArchivistGateway gateway = GatewayFactory.Create(new FixtureHandler());
        string channelId = FixtureHandler.Get("channel-small").Json.GetProperty("channel_id").GetString()!;
        VideoPageQuery query = new(VideoSort.Published, SortOrder.Desc, channelId);

        Page<Video> first = await gateway.GetVideoPageAsync(query, 1, Ct);
        Page<Video> second = await gateway.GetVideoPageAsync(query, 2, Ct);

        Assert.Equal(3, first.Items.Count);
        Assert.All(first.Items, v => Assert.Equal(channelId, v.ChannelId));
        Assert.True(second.IsPastEnd);
    }

    [Fact]
    public async Task An_unknown_channel_filter_gives_an_empty_list_not_an_error()
    {
        Page<Video> page = await GatewayFactory.Create(new FixtureHandler())
            .GetVideoPageAsync(
                new VideoPageQuery(VideoSort.Published, SortOrder.Desc, "UCaaaaaaaaaaaaaaaaaaaaaa"),
                1,
                Ct);

        Assert.True(page.IsPastEnd);
    }

    [Theory]
    [InlineData(VideoType.Short)]
    [InlineData(VideoType.Stream)]
    public async Task Type_filter_returns_only_that_type(VideoType type)
    {
        Page<Video> page = await GatewayFactory.Create(new FixtureHandler())
            .GetVideoPageAsync(
                new VideoPageQuery(VideoSort.Published, SortOrder.Desc, Type: type),
                1,
                Ct);

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, v => Assert.Equal(type, v.Type));
    }

    [Fact]
    public async Task Rejected_parameter_is_a_bad_request_with_field_errors()
    {
        TubeArchivistGateway gateway = GatewayFactory.Create(
            new FixtureHandler(_ => FixtureHandler.Respond("videos-sort-filesize")));

        TubeArchivistBadRequestException ex = await Assert.ThrowsAsync<TubeArchivistBadRequestException>(() =>
            gateway.GetVideoPageAsync(
                new VideoPageQuery(VideoSort.Downloaded, SortOrder.Desc),
                1,
                Ct));

        Assert.Contains("not a valid choice", Assert.Single(ex.Errors["sort"]), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_inactive_video_is_mapped()
    {
        JsonElement expected = FixtureHandler.Get("video-inactive").Json;
        string id = expected.GetProperty("youtube_id").GetString()!;

        Video? video = await GatewayFactory.Create(new FixtureHandler()).GetVideoAsync(id, Ct);

        Assert.NotNull(video);
        Assert.False(video.Active);
        Assert.Equal(VideoType.Video, video.Type);
        Assert.Equal($"{video.ChannelId}/{id}.mp4", video.MediaPath);
        Assert.Equal(
            DateTimeOffset.FromUnixTimeSeconds(expected.GetProperty("date_downloaded").GetInt64()),
            video.Downloaded);
        Assert.Contains(video.Tracks, t => t.Kind == MediaTrackKind.Video && t.Codec == "h264");
        Assert.Contains(video.Tracks, t => t.Kind == MediaTrackKind.Audio && t.Codec == "opus");
    }

    [Fact]
    public async Task A_4k_av1_video_keeps_its_track_details()
    {
        string id = FixtureHandler.Get("video-av1-4k").Json.GetProperty("youtube_id").GetString()!;

        Video? video = await GatewayFactory.Create(new FixtureHandler()).GetVideoAsync(id, Ct);

        MediaTrack track = Assert.Single(video!.Tracks, t => t.Kind == MediaTrackKind.Video);
        Assert.Equal(("av1", 3840, 2160), (track.Codec, track.Width, track.Height));
    }

    [Fact]
    public async Task Missing_items_are_null()
    {
        TubeArchivistGateway gateway = GatewayFactory.Create(new FixtureHandler());

        Assert.Null(await gateway.GetVideoAsync("aaaaaaaaaaa", Ct));
        Assert.Null(await gateway.GetChannelAsync("UCaaaaaaaaaaaaaaaaaaaaaa", Ct));
    }

    [Fact]
    public async Task A_channel_is_mapped_with_its_tabs_and_art()
    {
        string id = FixtureHandler.Get("channel-small").Json.GetProperty("channel_id").GetString()!;

        Channel? channel = await GatewayFactory.Create(new FixtureHandler()).GetChannelAsync(id, Ct);

        Assert.NotNull(channel);
        Assert.Contains(VideoType.Video, channel.Tabs);
        Assert.Equal($"/cache/channels/{id}_thumb.jpg", channel.ThumbPath);
        Assert.Equal($"/cache/channels/{id}_banner.jpg", channel.BannerPath);
    }

    // --- Requests the client builds ---

    [Theory, CombinatorialData]
    public void Video_list_urls_carry_every_parameter_in_ta_format(
        VideoSort sort,
        SortOrder order,
        [CombinatorialValues(null, VideoType.Video, VideoType.Short, VideoType.Stream)] VideoType? type,
        bool withChannel)
    {
        string? channel = withChannel ? "UCaaaaaaaaaaaaaaaaaaaaaa" : null;

        string url = TubeArchivistGateway.BuildVideoListUrl(
            new VideoPageQuery(sort, order, channel, type),
            7);

        Assert.StartsWith("api/video/?", url, StringComparison.Ordinal);
        Assert.Contains("sort=" + sort.ToString().ToLowerInvariant(), url, StringComparison.Ordinal);
        Assert.Contains("order=" + order.ToString().ToLowerInvariant(), url, StringComparison.Ordinal);
        Assert.Equal(
            withChannel,
            url.Contains("channel=UCaaaaaaaaaaaaaaaaaaaaaa", StringComparison.Ordinal));
        Assert.Equal(
            type is not null,
            url.Contains("type=", StringComparison.Ordinal));
        if (type is not null)
        {
            Assert.Contains(
                "type=" + type switch { VideoType.Video => "videos", VideoType.Short => "shorts", _ => "streams" },
                url,
                StringComparison.Ordinal);
        }

        Assert.EndsWith("&page=7", url, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("../../../etc")]
    [InlineData("abc?x=1&y=2")]
    [InlineData("abcdefghij")]
    [InlineData("abcdefghijkl")]
    [InlineData("abcdefghijk\n")]
    [InlineData("")]
    public async Task Malformed_video_ids_are_refused_without_calling_TA(string id)
    {
        FixtureHandler fake = new();

        await Assert.ThrowsAsync<ArgumentException>(() => GatewayFactory.Create(fake).GetVideoAsync(id, Ct));
        Assert.Empty(fake.Requests);
    }

    [Theory]
    [InlineData("UC../../../../etc/passwd...")]
    [InlineData("UCaaaaaaaaaaaaaaaaaaaaa&x")]
    [InlineData("XXaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("UCaaaaaaaaaaaaaaaaaaaaaa\n")]
    public async Task Malformed_channel_ids_are_refused_without_calling_TA(string id)
    {
        FixtureHandler fake = new();
        TubeArchivistGateway gateway = GatewayFactory.Create(fake);

        await Assert.ThrowsAsync<ArgumentException>(() => gateway.GetChannelAsync(id, Ct));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            gateway.GetVideoPageAsync(
                new VideoPageQuery(VideoSort.Published, SortOrder.Desc, id),
                1,
                Ct));
        Assert.Empty(fake.Requests);
    }

    // --- Cache files ---

    [Theory]
    [InlineData("/cache/videos/a/abcdefghijk.jpg")]
    [InlineData("/cache/channels/UCaaaaaaaaaaaaaaaaaaaaaa_banner.jpg")]
    public async Task Cache_images_are_fetched(string path)
    {
        FixtureHandler fake = new(
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) });

        await using Stream? stream = await GatewayFactory.Create(fake).OpenCacheFileAsync(path, Ct);

        Assert.Equal([1, 2, 3], ((MemoryStream)stream!).ToArray());
        Assert.Equal(path, Assert.Single(fake.Requests).RequestUri!.AbsolutePath);
    }

    [Theory]
    [InlineData("/cache/videos/a/../../../../etc/passwd")]
    [InlineData("/cache/videos/a/abcdefghijk.jpg/../../api/ping")]
    [InlineData("/cache/videos/a%2f..%2f..%2fapi/abcdefghijk.jpg")]
    [InlineData("/cache/videos/../../../abcdefghijk.jpg")]
    [InlineData("/cache/videos/a/b/abcdefghijk.jpg")]
    [InlineData("/cache/videos//abcdefghijk.jpg")]
    [InlineData("/cache/channels/../UCaaaaaaaaaaaaaaaaaaaaaa_thumb.jpg")]
    [InlineData("/cache/channels/UCaaaaaaaaaaaaaaaaaaaaaa_thumb.jpg?x=1")]
    [InlineData("/cache/videos/a/abcdefghijk.jpg\n")]
    [InlineData("/youtube/UCaaaaaaaaaaaaaaaaaaaaaa/abcdefghijk.mp4")]
    [InlineData("/api/video/abcdefghijk/")]
    [InlineData("https://elsewhere.example/cache/videos/a/abcdefghijk.jpg")]
    [InlineData("cache/videos/a/abcdefghijk.jpg")]
    public async Task Anything_but_a_cache_image_path_is_refused_without_calling_TA(string path)
    {
        FixtureHandler fake = new();

        await Assert.ThrowsAsync<ArgumentException>(() => GatewayFactory.Create(fake).OpenCacheFileAsync(path, Ct));
        Assert.Empty(fake.Requests);
    }

    [Fact]
    public async Task A_missing_cache_image_is_null()
    {
        FixtureHandler fake = new(
            _ => FixtureHandler.Respond(HttpStatusCode.NotFound, "<h1>Not Found</h1>", "text/html"));

        Assert.Null(await GatewayFactory.Create(fake).OpenCacheFileAsync("/cache/videos/a/abcdefghijk.jpg", Ct));
    }

    // --- Failure mapping ---

    public enum Failure
    {
        Unauthorized,
        Forbidden,
        BadGateway,
        ServiceUnavailable,
        Teapot,
        NotJson,
        NetworkDown,
        CircuitOpen,
    }

    [Theory, CombinatorialData]
    public async Task Failures_are_typed(Failure failure)
    {
        FixtureHandler fake = new(_ => failure switch
        {
            Failure.Unauthorized => FixtureHandler.Respond(HttpStatusCode.Unauthorized, "{}"),
            Failure.Forbidden => FixtureHandler.Respond(HttpStatusCode.Forbidden, "{}"),
            Failure.BadGateway => FixtureHandler.Respond(HttpStatusCode.BadGateway, "<html/>", "text/html"),
            Failure.ServiceUnavailable => FixtureHandler.Respond(
                HttpStatusCode.ServiceUnavailable,
                "<html/>",
                "text/html"),
            Failure.Teapot => FixtureHandler.Respond((HttpStatusCode)418, "{}"),
            Failure.NotJson => FixtureHandler.Respond(HttpStatusCode.OK, "<html>login</html>", "text/html"),
            Failure.CircuitOpen => throw new BrokenCircuitException("The circuit is now open."),
            _ => throw new HttpRequestException("connection refused", null, HttpStatusCode.ServiceUnavailable),
        });

        TubeArchivistException ex = await Assert.ThrowsAnyAsync<TubeArchivistException>(
            () => GatewayFactory.Create(fake).PingAsync(Ct));

        Type expected = failure switch
        {
            Failure.Unauthorized or Failure.Forbidden => typeof(TubeArchivistAuthException),
            Failure.BadGateway or Failure.ServiceUnavailable => typeof(TubeArchivistServerException),
            Failure.Teapot => typeof(TubeArchivistException),
            Failure.NotJson => typeof(TubeArchivistProtocolException),
            _ => typeof(TubeArchivistUnavailableException),
        };
        Assert.Equal(expected, ex.GetType());
        Assert.DoesNotContain(GatewayFactory.Token, ex.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_paginated_answer_without_page_size_is_a_protocol_error()
    {
        FixtureHandler fake = new(
            _ => FixtureHandler.Respond(HttpStatusCode.OK, """{"data": [], "paginate": {}}"""));

        await Assert.ThrowsAsync<TubeArchivistProtocolException>(
            () => GatewayFactory.Create(fake).GetChannelPageAsync(1, Ct));
    }

    // --- Broken documents inside a page ---

    [Fact]
    public async Task Broken_documents_are_rejected_one_by_one_and_the_rest_of_the_page_is_kept()
    {
        string good = FixtureHandler.Get("video-inactive").Body;
        string badPath = good.Replace(
            "\"media_url\": \"/youtube/",
            "\"media_url\": \"/youtube/../",
            StringComparison.Ordinal);
        string badType = good.Replace("\"title\": \"", "\"title\": 42, \"x\": \"", StringComparison.Ordinal);
        Assert.NotEqual(good, badPath);
        Assert.NotEqual(good, badType);
        string body = $$$"""
            {
              "data": [{{{good}}}, {{{badPath}}}, {{{badType}}}],
              "paginate": { "page_size": 100, "total_hits": 3, "max_hits": false }
            }
            """;
        FixtureHandler fake = new(_ => FixtureHandler.Respond(HttpStatusCode.OK, body));

        Page<Video> page = await GatewayFactory.Create(fake)
            .GetVideoPageAsync(
                new VideoPageQuery(VideoSort.Downloaded, SortOrder.Desc),
                1,
                Ct);

        Assert.Single(page.Items);
        Assert.Equal(2, page.Rejected.Count);
        Assert.All(page.Rejected, r => Assert.Equal(page.Items[0].Id, r.Id));
        Assert.Contains(page.Rejected[0].Errors, e => e.Field == "media_url");
        Assert.Contains(
            page.Rejected[1].Errors,
            e => e.Message.Contains("unexpected JSON type", StringComparison.Ordinal));
        Assert.False(page.IsPastEnd);
    }

    [Fact]
    public async Task A_page_of_only_broken_documents_is_not_past_the_end()
    {
        string body
            = """{"data": [{"youtube_id": "x"}], "paginate": {"page_size": 100, "total_hits": 1, "max_hits": false}}""";
        FixtureHandler fake = new(_ => FixtureHandler.Respond(HttpStatusCode.OK, body));

        Page<Video> page = await GatewayFactory.Create(fake)
            .GetVideoPageAsync(
                new VideoPageQuery(VideoSort.Downloaded, SortOrder.Desc),
                1,
                Ct);

        Assert.Empty(page.Items);
        Assert.Null(Assert.Single(page.Rejected).Id);
        Assert.False(page.IsPastEnd);
    }

    [Fact]
    public async Task A_broken_single_video_throws_validation_exception()
    {
        FixtureHandler fake = new(
            _ => FixtureHandler.Respond(HttpStatusCode.OK, """{"youtube_id": "abcdefghijk"}"""));

        ValidationException ex = await Assert.ThrowsAsync<ValidationException>(
            () => GatewayFactory.Create(fake).GetVideoAsync("abcdefghijk", Ct));

        Assert.Equal("TubeArchivist video abcdefghijk", ex.Subject);
    }

    private static IEnumerable<string> Ids(Page<Channel> page) => page.Items.Select(c => c.Id);

    private static IEnumerable<string> Ids(Page<Video> page) => page.Items.Select(v => v.Id);
}
