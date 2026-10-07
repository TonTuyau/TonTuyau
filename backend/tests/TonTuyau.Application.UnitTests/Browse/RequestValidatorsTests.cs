using TonTuyau.Application.Browse;
using TonTuyau.Application.Validation;

namespace TonTuyau.Application.UnitTests.Browse;

public sealed class RequestValidatorsTests
{
    private const string Channel = "UCaaaaaaaaaaaaaaaaaaaaaa";

    [Theory, CombinatorialData]
    public void Video_lists_check_type_sort_exclude_and_size(
        [CombinatorialValues(null, "video", "short", "stream", "movie", "Video")] string? type,
        [CombinatorialValues(null, "latest", "popular", "oldest", "relevance", "views")] string? sort,
        [CombinatorialValues(null, "abcdefghijk", "abc", "../../x/abc")] string? exclude,
        [CombinatorialValues(null, 1, 24, 100, 0, -1, 101)] int? size)
    {
        string[] expected =
        [
            .. Bad("exclude", exclude is "abc" or "../../x/abc"),
            .. Bad("size", size is < 1 or > 100),
            .. Bad("sort", sort is "relevance" or "views"),
            .. Bad("type", type is "movie" or "Video"),
        ];

        VideoListRequest request = new(type, sort, exclude, null, size);

        Assert.Equal(
            expected,
            FieldsRefused(new VideoListRequestValidator(), request));
    }

    [Theory, CombinatorialData]
    public void A_channel_tab_checks_the_channel_id_first(
        [CombinatorialValues("tomcooks", "UC../../../etc/passwdxxxx", "", Channel + "\n")] string id,
        [CombinatorialValues(null, "movie")] string? type)
    {
        ChannelVideosRequest request = new(id, type, null, null, null, null);

        ValidationException error = Assert.Throws<ValidationException>(
            () => new ChannelVideosRequestValidator().Validate(request));

        Assert.Equal(["id"], error.Errors.Select(e => e.Field));
    }

    /// <summary>One rule for both lists: the library and a channel's tab answer the same to the same parameters.</summary>
    [Theory, CombinatorialData]
    public void The_library_and_a_channel_tab_check_their_parameters_alike(
        [CombinatorialValues(null, "short", "movie")] string? type,
        [CombinatorialValues(null, "popular", "views")] string? sort,
        [CombinatorialValues(null, "abcdefghijk", "abc")] string? exclude,
        [CombinatorialValues(null, 24, 0)] int? size)
    {
        VideoListRequest library = new(type, sort, exclude, null, size);
        ChannelVideosRequest tab = new(Channel, type, sort, exclude, null, size);

        Assert.Equal(
            FieldsRefused(new VideoListRequestValidator(), library),
            FieldsRefused(new ChannelVideosRequestValidator(), tab));
    }

    [Theory, CombinatorialData]
    public void A_channel_tab_checks_the_video_it_leaves_out(
        [CombinatorialValues(null, "abcdefghijk", "abc", "../../x/abc", "abcdefghijk\n")] string? exclude,
        [CombinatorialValues(null, "short", "movie")] string? type)
    {
        bool badExclude = exclude is "abc" or "../../x/abc" or "abcdefghijk\n";
        string[] expected = [.. Bad("exclude", badExclude), .. Bad("type", type == "movie")];
        ChannelVideosRequest request = new(Channel, type, null, exclude, null, null);

        Assert.Equal(
            expected,
            FieldsRefused(new ChannelVideosRequestValidator(), request));
    }

    [Theory, CombinatorialData]
    public void Channel_lists_check_the_filter_sort_and_size(
        [CombinatorialValues(null, "tom", "  ", "x")] string? q,
        [CombinatorialValues(null, "name", "subscribers", "latest")] string? sort,
        [CombinatorialValues(null, 24, 0)] int? size)
    {
        string[] expected = [.. Bad("size", size == 0), .. Bad("sort", sort == "latest")];
        ChannelListRequest request = new(q, sort, null, size);

        Assert.Equal(
            expected,
            FieldsRefused(new ChannelListRequestValidator(), request));
    }

    [Fact]
    public void A_channel_filter_has_a_maximum_length()
    {
        ChannelListRequest request = new(new string('a', 101), null, null, null);

        Assert.Equal(
            ["q"],
            FieldsRefused(new ChannelListRequestValidator(), request));
    }

    [Theory, CombinatorialData]
    public void Search_checks_its_text_kind_and_sort(
        [CombinatorialValues(null, "", "   ", "creme", "a")] string? q,
        [CombinatorialValues(null, "all", "videos", "channels", "playlists")] string? kind,
        [CombinatorialValues(null, "relevance", "latest", "popular", "oldest")] string? sort)
    {
        string[] expected =
        [
            .. Bad("kind", kind == "playlists"),
            .. Bad("q", string.IsNullOrWhiteSpace(q)),
            .. Bad("sort", sort == "oldest" || (kind == "channels" && sort is "latest" or "popular")),
        ];

        VideoSearchRequest request = Search(q, kind: kind, sort: sort);

        Assert.Equal(
            expected,
            FieldsRefused(new VideoSearchRequestValidator(), request));
    }

    [Theory, CombinatorialData]
    public void Video_filters_only_apply_when_videos_are_searched(
        [CombinatorialValues(null, "all", "videos", "channels")] string? kind,
        [CombinatorialValues("type", "channel", "published", "duration")] string filter)
    {
        VideoSearchRequest request = filter switch
        {
            "type" => Search("x", kind: kind, type: "short"),
            "channel" => Search("x", kind: kind, channel: Channel),
            "published" => Search("x", kind: kind, published: "week"),
            _ => Search("x", kind: kind, duration: "long"),
        };

        Assert.Equal(
            Bad(filter, kind == "channels"),
            FieldsRefused(new VideoSearchRequestValidator(), request));
    }

    [Theory, CombinatorialData]
    public void Search_filters_are_checked(
        [CombinatorialValues(null, "video", "clip")] string? type,
        [CombinatorialValues(null, Channel, "tomcooks", "UCaaaaaaaaaaaaaaaaaaaaa/")] string? channel,
        [CombinatorialValues(null, "short", "medium", "long", "epic")] string? duration)
    {
        string[] expected =
        [
            .. Bad("channel", channel is "tomcooks" or "UCaaaaaaaaaaaaaaaaaaaaa/"),
            .. Bad("duration", duration == "epic"),
            .. Bad("type", type == "clip"),
        ];

        VideoSearchRequest request = Search("x", type: type, channel: channel, duration: duration);

        Assert.Equal(
            expected,
            FieldsRefused(new VideoSearchRequestValidator(), request));
    }

    [Theory]
    [InlineData("today", false)]
    [InlineData("week", false)]
    [InlineData("month", false)]
    [InlineData("year", false)]
    [InlineData("Today", true)]
    [InlineData("hour", true)]
    [InlineData("2026-01-01", true)]
    [InlineData("", true)]
    public void The_upload_date_filter_is_a_named_period(string published, bool refused)
    {
        VideoSearchRequest request = Search("x", published: published);

        Assert.Equal(
            Bad("published", refused),
            FieldsRefused(new VideoSearchRequestValidator(), request));
    }

    [Theory]
    [InlineData(200, false)]
    [InlineData(201, true)]
    public void Search_text_has_a_maximum_length(int length, bool refused)
    {
        VideoSearchRequest request = Search(new string('a', length));

        Assert.Equal(
            Bad("q", refused),
            FieldsRefused(new VideoSearchRequestValidator(), request));
    }

    [Theory, CombinatorialData]
    public void Suggestions_check_text_and_size(
        [CombinatorialValues(null, " ", "c", "tom c")] string? q,
        [CombinatorialValues(null, 1, 8, 20, 0, 21)] int? size)
    {
        string[] expected = [.. Bad("q", string.IsNullOrWhiteSpace(q)), .. Bad("size", size is 0 or 21)];

        SuggestRequest request = new(q, size);

        Assert.Equal(
            expected,
            FieldsRefused(new SuggestRequestValidator(), request));
    }

    [Fact]
    public void Every_broken_rule_is_reported_at_once_with_a_readable_message()
    {
        VideoListRequest request = new("movie", "views", "x", null, 0);

        ValidationException error = Assert.Throws<ValidationException>(
            () => new VideoListRequestValidator().Validate(request));

        Assert.Equal(
            [
                ("type", "must be one of video, short, stream."),
                ("sort", "must be one of latest, popular, oldest."),
                ("exclude", "isn't a video id."),
                ("size", "must be between 1 and 100."),
            ],
            error.Errors.Select(e => (e.Field, e.Message)));
    }

    [Theory]
    [InlineData("abcdefghijk", true)]
    [InlineData("_-0123456789", false)]
    [InlineData("abc", false)]
    [InlineData("../../etc/x", false)]
    [InlineData("abcdefghij/", false)]
    [InlineData("abcdefghijk\n", false)]
    public void Video_ids_from_urls_are_checked(string id, bool valid)
    {
        if (valid)
        {
            RequestRules.RequireVideoId(id);
        }
        else
        {
            ValidationException error = Assert.Throws<ValidationException>(() => RequestRules.RequireVideoId(id));
            Assert.Equal("id", error.Errors[0].Field);
        }
    }

    private static VideoSearchRequest Search(
        string? q,
        string? kind = null,
        string? type = null,
        string? channel = null,
        string? published = null,
        string? duration = null,
        string? sort = null) =>
        new(q, kind, type, channel, published, duration, sort, null, null);

    private static string[] Bad(string field, bool refused) => refused ? [field] : [];

    /// <returns>The fields refused, in alphabetical order; none when the request is valid.</returns>
    private static string[] FieldsRefused<T>(IValidator<T> validator, T request)
    {
        try
        {
            validator.Validate(request);
            return [];
        }
        catch (ValidationException ex)
        {
            Assert.All(ex.Errors, e => Assert.EndsWith(".", e.Message, StringComparison.Ordinal));
            return ex.Errors
                .Select(e => e.Field)
                .Distinct()
                .Order(StringComparer.Ordinal)
                .ToArray();
        }
    }
}
