using System.Text;
using System.Text.Json;
using TonTuyau.IntegrationTests.Support;

namespace TonTuyau.IntegrationTests.Steps;

/// <summary>
/// What YouTube answers the refreshes (1.5), through the fake yt-dlp (<see cref="YouTubeSteps.Answers"/>). Videos and
/// channels are named as in the fake TubeArchivist ("alpha-01", "alpha").
/// </summary>
[Binding]
public sealed class YouTubeRefreshSteps(YouTubeSteps youTube, AuthSteps auth)
{
    private static readonly string[] Tags = ["youtube"];
    private static readonly string[] Categories = ["Education"];

    [Given("YouTube has the video {string}: the title {string}, {int} views")]
    public Task VideoFound(string name, string title, int views) => WriteAsync(name, VideoAnswer(name, title, views));

    /// <summary>Its comments: "Comment 1"… each answered once by the channel ("Reply 1"…), the first one pinned.</summary>
    [Given("YouTube has the video {string} with {int} comments and {int} chapters")]
    public Task VideoWithComments(string name, int threads, int chapters)
    {
        Dictionary<string, object> answer = VideoAnswer(name, "Avec commentaires", 10);
        answer["comments"] = Enumerable
            .Range(1, threads)
            .SelectMany(i => new object[]
            {
                new Dictionary<string, object>
                {
                    ["id"] = $"c{i}",
                    ["parent"] = "root",
                    ["text"] = $"Comment {i}",
                    ["author"] = $"@viewer{i}",
                    ["timestamp"] = 1_790_000_000 + i,
                    ["like_count"] = 100 - i,
                    ["is_pinned"] = i == 1,
                },
                new Dictionary<string, object>
                {
                    ["id"] = $"c{i}.r1",
                    ["parent"] = $"c{i}",
                    ["text"] = $"Reply {i}",
                    ["author"] = "@alpha",
                    ["author_is_uploader"] = true,
                },
            })
            .ToArray();
        answer["chapters"] = Enumerable
            .Range(0, chapters)
            .Select(i => new Dictionary<string, object>
            {
                ["start_time"] = i * 30.0,
                ["end_time"] = (i + 1) * 30.0,
                ["title"] = $"Part {i + 1}",
            })
            .ToArray();
        return WriteAsync(name, answer);
    }

    /// <summary>A video's metadata; its best thumbnail is a WebP when its title says "WebP", else a JPEG.</summary>
    private static Dictionary<string, object> VideoAnswer(string name, string title, int views)
    {
        string id = FakeTubeArchivist.VideoId(name);
        string best = title.Contains("WebP", StringComparison.Ordinal) ? "maxresdefault.webp" : "maxresdefault.jpg";
        return new Dictionary<string, object>
        {
            ["id"] = id,
            ["title"] = title,
            ["description"] = "From YouTube",
            ["channel_id"] = FakeTubeArchivist.ChannelId(name.Split('-')[0]),
            ["channel"] = "Alpha on YouTube",
            ["timestamp"] = 1_790_000_000,
            ["duration"] = 120,
            ["view_count"] = views,
            ["like_count"] = 12,
            ["tags"] = Tags,
            ["categories"] = Categories,
            ["comment_count"] = 3,
            ["thumbnails"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["url"] = $"https://i.ytimg.com/vi/{id}/default.jpg",
                    ["width"] = 120,
                    ["id"] = "0",
                },
                new Dictionary<string, object>
                {
                    ["url"] = $"https://i.ytimg.com/vi/{id}/{best}",
                    ["width"] = 1280,
                    ["id"] = "40",
                },
            },
        };
    }

    private Task WriteAsync(string video, Dictionary<string, object> answer) =>
        File.WriteAllTextAsync(
            Path.Combine(youTube.Answers, FakeTubeArchivist.VideoId(video) + ".json"),
            JsonSerializer.Serialize(answer));

    [Given("YouTube has the channel {string}: the name {string}, {int} subscribers")]
    public Task ChannelFound(string name, string title, int subscribers)
    {
        string id = FakeTubeArchivist.ChannelId(name);
        return File.WriteAllTextAsync(
            Path.Combine(youTube.Answers, id + ".json"),
            JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["channel_id"] = id,
                ["channel"] = title,
                ["description"] = "From YouTube",
                ["channel_follower_count"] = subscribers,
                ["tags"] = Tags,
                ["thumbnails"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["url"] = "https://yt3.googleusercontent.com/avatar=s900",
                        ["id"] = "avatar_uncropped",
                    },
                    new Dictionary<string, object>
                    {
                        ["url"] = "https://yt3.googleusercontent.com/missing-banner",
                        ["id"] = "banner_uncropped",
                    },
                },
            }));
    }

    [Given("YouTube says of the video {string}: {string}")]
    public Task VideoFails(string name, string error) =>
        File.WriteAllTextAsync(Path.Combine(youTube.Answers, FakeTubeArchivist.VideoId(name) + ".err"), error);

    [Given("YouTube says of the channel {string}: {string}")]
    public Task ChannelFails(string name, string error) =>
        File.WriteAllTextAsync(Path.Combine(youTube.Answers, FakeTubeArchivist.ChannelId(name) + ".err"), error);

    /// <summary>The YouTube page's setting (off by default): the top 500, sent with the admin's session.</summary>
    [Given("reading comments is on")]
    public Task CommentsOn() => SendSettingsAsync("{\"comments\":\"top\",\"commentsMax\":500}");

    /// <summary>The YouTube page's pause between videos at 0 (10 s by default, about 5 to 15).</summary>
    [Given("there's no pause between videos")]
    public Task NoPause() => SendSettingsAsync("{\"comments\":\"off\",\"pauseSeconds\":0}");

    [When("I send the YouTube settings {string} with my session")]
    public async Task SendSettingsAsync(string body)
    {
        using HttpRequestMessage request = new(HttpMethod.Put, new Uri("/api/admin/youtube/settings", UriKind.Relative))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        await auth.SendAsync(request);
    }

    [When("I refresh the video {string} with my session")]
    public Task RefreshVideo(string name) =>
        auth.PostWithSession($"/api/admin/tasks/refreshVideo/runs?target={FakeTubeArchivist.VideoId(name)}");

    [When("I refresh the channel {string} with my session")]
    public Task RefreshChannel(string name) =>
        auth.PostWithSession($"/api/admin/tasks/refreshChannel/runs?target={FakeTubeArchivist.ChannelId(name)}");

    [When("I refresh the channel {string} and its videos with my session")]
    public Task RefreshChannelVideos(string name) =>
        auth.PostWithSession(
            $"/api/admin/tasks/refreshChannelVideos/runs?target={FakeTubeArchivist.ChannelId(name)}");
}
