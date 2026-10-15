using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TonTuyau.Application.Configuration;
using TonTuyau.Application.YouTube;

namespace TonTuyau.YouTube.UnitTests;

/// <summary>
/// The client against a fake yt-dlp (Fixtures/fake-yt-dlp.sh) that answers from the fixture files: what YouTube would
/// say, shaped as yt-dlp prints it.
/// </summary>
public sealed class YouTubeClientTests
{
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");

    private static YouTubeClient Client(string ytdlp = "fake-yt-dlp.sh", string deno = "fake-deno.sh")
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ConfigKeys.YtdlpPath] = Path.Combine(Fixtures, ytdlp),
                [ConfigKeys.DenoPath] = Path.Combine(Fixtures, deno),
            })
            .Build();
        TonTuyauOptions options = TonTuyauOptionsReader.Read(configuration);
        return new YouTubeClient(options, NullLogger<YouTubeClient>.Instance);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_video_found_comes_with_its_metadata()
    {
        YouTubeLookup<YouTubeVideo> lookup = await Client().GetVideoAsync("k3Fq8xZ0aQw", null, Token);

        Assert.Equal(YouTubeAnswer.Found, lookup.Answer);
        Assert.Null(lookup.Reason);
        YouTubeVideo video = lookup.Item!;
        Assert.Equal("Why Paris sewers still follow Belgrand’s 1850 plan", video.Title);
        Assert.Equal(
            "Paris still drains through galleries laid out in the 1850s.\n\n0:00 A street drain",
            video.Description);
        Assert.Equal("UCx7Lm2QpB0vK9sTq4aZ1wYc", video.ChannelId);
        Assert.Equal("Under the City", video.ChannelName);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1788526800), video.Published);
        Assert.Equal(1192, video.DurationSeconds);
        Assert.Equal(212431, video.Views);
        Assert.Equal(18034, video.Likes);
        Assert.Equal(["paris", "infrastructure", "history"], video.Tags);
        Assert.Equal(["Education"], video.Categories);
        Assert.Equal("not_live", video.LiveStatus);
        // An entry without a URL is left out.
        Assert.Equal(
            [
                new YouTubeThumbnail("https://i.ytimg.com/vi/k3Fq8xZ0aQw/default.jpg", 120, 90),
                new YouTubeThumbnail("https://i.ytimg.com/vi_webp/k3Fq8xZ0aQw/maxresdefault.webp", 1280, 720),
            ],
            video.Thumbnails);
    }

    [Fact]
    public async Task Without_a_timestamp_the_day_of_publication_is_midnight_utc_and_missing_lists_are_empty()
    {
        YouTubeVideo video = (await Client().GetVideoAsync("Day0000000A", null, Token)).Item!;

        Assert.Equal(new DateTimeOffset(2024, 2, 29, 0, 0, 0, TimeSpan.Zero), video.Published);
        Assert.Equal("", video.Description);
        Assert.Empty(video.Tags);
        Assert.Empty(video.Thumbnails);
        Assert.Null(video.DurationSeconds);
    }

    [Fact]
    public async Task Yt_dlp_is_asked_for_one_video_metadata_only_without_config_cache_or_download()
    {
        await Client().GetVideoAsync("k3Fq8xZ0aQw", null, Token);

        string call = File
            .ReadAllLines(Path.Combine(Fixtures, "calls.log"))
            .Last(l => l.Contains("watch?v=k3Fq8xZ0aQw", StringComparison.Ordinal));
        Assert.Contains("--ignore-config", call, StringComparison.Ordinal);
        Assert.Contains("--no-cache-dir", call, StringComparison.Ordinal);
        Assert.Contains("--skip-download", call, StringComparison.Ordinal);
        Assert.Contains("--dump-single-json", call, StringComparison.Ordinal);
        Assert.Contains("--no-playlist", call, StringComparison.Ordinal);
        Assert.EndsWith("https://www.youtube.com/watch?v=k3Fq8xZ0aQw", call, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Removed00001", "Video unavailable. This video has been removed by the uploader")]
    [InlineData("Private0001", "Private video. Sign in if you've been granted access to this video")]
    public async Task YouTube_saying_it_was_removed_or_made_private_is_gone(string id, string reason)
    {
        YouTubeLookup<YouTubeVideo> lookup = await Client().GetVideoAsync(id, null, Token);

        Assert.Equal(YouTubeAnswer.Gone, lookup.Answer);
        Assert.Equal(reason, lookup.Reason);
        Assert.Null(lookup.Item);
    }

    [Theory]
    [InlineData("BotCheck001", "Sign in to confirm you’re not a bot")]
    [InlineData("Unavail0001", "Video unavailable")]
    [InlineData("Silent00001", "yt-dlp failed without saying why (exit code 1).")]
    [InlineData("Other000001", "yt-dlp's answer isn't this video's metadata.")]
    [InlineData("NotJson0001", "yt-dlp's answer isn't this video's metadata.")]
    public async Task Anything_else_is_a_failed_check_never_gone(string id, string reason)
    {
        YouTubeLookup<YouTubeVideo> lookup = await Client().GetVideoAsync(id, null, Token);

        Assert.Equal(YouTubeAnswer.CheckFailed, lookup.Answer);
        Assert.StartsWith(reason, lookup.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Comments_come_when_asked_answers_named_by_their_parent()
    {
        YouTubeVideo video = (await Client().GetVideoAsync(
            "Comments001",
            new YouTubeCommentsRequest(500),
            Token)).Item!;

        Assert.Equal(1342, video.CommentCount);
        Assert.Equal(
            [
                new YouTubeComment(
                    "UgzA1",
                    null,
                    "The 1850 plan part is fascinating.\nMore please!",
                    "@claire",
                    "UCclaire000000000000000",
                    "https://yt3.ggpht.com/claire=s88",
                    DateTimeOffset.FromUnixTimeSeconds(1788530400),
                    214,
                    Pinned: true,
                    ByUploader: false,
                    Hearted: true),
                new YouTubeComment(
                    "UgzA1.Reply1",
                    "UgzA1",
                    "Part 2 is coming.",
                    "@underthecity",
                    "UCx7Lm2QpB0vK9sTq4aZ1wYc",
                    null,
                    DateTimeOffset.FromUnixTimeSeconds(1788534000),
                    31,
                    Pinned: false,
                    ByUploader: true,
                    Hearted: false),
            ],
            video.Comments);
    }

    [Fact]
    public async Task Yt_dlp_is_asked_for_that_many_comments_answers_included()
    {
        await Client().GetVideoAsync("Comments001", new YouTubeCommentsRequest(250), Token);

        Assert.Contains(
            File
                .ReadAllLines(Path.Combine(Fixtures, "calls.log"))
                .Where(l => l.Contains("watch?v=Comments001", StringComparison.Ordinal)),
            l => l.Contains(
                "--write-comments --extractor-args youtube:max_comments=250,all,all,all",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Without_asking_there_are_no_comments_and_yt_dlp_isnt_asked_for_them()
    {
        YouTubeVideo video = (await Client().GetVideoAsync("Comments001", null, Token)).Item!;
        await Client().GetVideoAsync("Day0000000A", null, Token);

        Assert.Null(video.Comments);
        Assert.Equal(1342, video.CommentCount);
        string call = File
            .ReadAllLines(Path.Combine(Fixtures, "calls.log"))
            .Last(l => l.Contains("watch?v=Day0000000A", StringComparison.Ordinal));
        Assert.DoesNotContain("--write-comments", call, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Asked_for_comments_a_video_without_any_has_an_empty_list()
    {
        YouTubeVideo video = (await Client().GetVideoAsync(
            "NoComment01",
            new YouTubeCommentsRequest(500),
            Token)).Item!;

        Assert.Equal([], video.Comments);
    }

    [Fact]
    public async Task Without_yt_dlp_the_check_failed()
    {
        YouTubeLookup<YouTubeVideo> lookup = await Client(ytdlp: "no-such-program")
            .GetVideoAsync("k3Fq8xZ0aQw", null, Token);

        Assert.Equal(YouTubeAnswer.CheckFailed, lookup.Answer);
        Assert.Equal("yt-dlp can't be run here.", lookup.Reason);
    }

    [Fact]
    public async Task The_caller_canceling_stops_the_question()
    {
        using CancellationTokenSource cancel = new(TimeSpan.FromMilliseconds(500));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Client().GetVideoAsync("Slow0000001", null, cancel.Token));
    }

    [Fact]
    public async Task A_channel_found_comes_with_its_metadata_and_pictures()
    {
        YouTubeLookup<YouTubeChannel> lookup = await Client().GetChannelAsync("UCx7Lm2QpB0vK9sTq4aZ1wYc", Token);

        Assert.Equal(YouTubeAnswer.Found, lookup.Answer);
        YouTubeChannel channel = lookup.Item!;
        Assert.Equal("Under the City", channel.Name);
        Assert.Equal("Infrastructure, from below.", channel.Description);
        Assert.Equal("@underthecity", channel.Handle);
        Assert.Equal(412000, channel.Subscribers);
        Assert.Equal(["engineering", "cities"], channel.Tags);
        Assert.Equal(2, channel.Thumbnails.Count);

        string call = File
            .ReadAllLines(Path.Combine(Fixtures, "calls.log"))
            .Last(l => l.Contains("channel/UCx7Lm2QpB0vK9sTq4aZ1wYc", StringComparison.Ordinal));
        Assert.Contains("--flat-playlist --playlist-items 0", call, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_terminated_channel_is_gone()
    {
        YouTubeLookup<YouTubeChannel> lookup = await Client().GetChannelAsync("UCTerminated000000000000", Token);

        Assert.Equal(YouTubeAnswer.Gone, lookup.Answer);
        Assert.StartsWith("This account has been terminated", lookup.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_tools_say_their_versions()
    {
        Assert.Equal(new YouTubeTools("2026.08.19", "2.9.7"), await Client().GetToolsAsync(Token));
    }

    [Fact]
    public async Task A_missing_tool_has_no_version()
    {
        YouTubeTools tools = await Client(ytdlp: "no-such-program", deno: "no-such-deno").GetToolsAsync(Token);

        Assert.Equal(new YouTubeTools(null, null), tools);
    }
}
