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
    public Task VideoFound(string name, string title, int views)
    {
        string id = FakeTubeArchivist.VideoId(name);
        return File.WriteAllTextAsync(
            Path.Combine(youTube.Answers, id + ".json"),
            JsonSerializer.Serialize(new Dictionary<string, object>
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
            }));
    }

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
            }));
    }

    [Given("YouTube says of the video {string}: {string}")]
    public Task VideoFails(string name, string error) =>
        File.WriteAllTextAsync(Path.Combine(youTube.Answers, FakeTubeArchivist.VideoId(name) + ".err"), error);

    [Given("YouTube says of the channel {string}: {string}")]
    public Task ChannelFails(string name, string error) =>
        File.WriteAllTextAsync(Path.Combine(youTube.Answers, FakeTubeArchivist.ChannelId(name) + ".err"), error);

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
