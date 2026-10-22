using System.Text.Json;
using TonTuyau.IntegrationTests.Support;

namespace TonTuyau.IntegrationTests.Steps;

/// <summary>
/// The YouTube syncs (2.0, B5): what a channel's tabs list, through the fake yt-dlp (<see cref="YouTubeSteps.Answers"/>),
/// and starting a sync from the Tasks page. Videos and channels are named as in the fake TubeArchivist ("alpha-01").
/// </summary>
[Binding]
public sealed class YouTubeSyncSteps(YouTubeSteps youTube, AuthSteps auth)
{
    /// <param name="names">Newest first, by commas: "alpha-05, alpha-04 (upcoming), alpha-02"; each titled "Video …".</param>
    /// <param name="tab">videos | shorts | streams.</param>
    [Given("YouTube lists {string} on the {word} tab of the channel {string}")]
    public Task Lists(string names, string tab, string channel)
    {
        IEnumerable<string> lines = names
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(entry =>
            {
                bool upcoming = entry.EndsWith(" (upcoming)", StringComparison.Ordinal);
                string name = upcoming ? entry[..^" (upcoming)".Length] : entry;
                return JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["id"] = FakeTubeArchivist.VideoId(name),
                    ["live_status"] = upcoming ? "is_upcoming" : "not_live",
                    ["title"] = $"Video {name}",
                });
            });
        return File.WriteAllLinesAsync(Path.Combine(youTube.Answers, $"{Of(channel)}.{tab}.jsonl"), lines);
    }

    [Given("YouTube says of the {word} tab of the channel {string}: {string}")]
    public Task TabFails(string tab, string channel, string error) =>
        File.WriteAllTextAsync(Path.Combine(youTube.Answers, $"{Of(channel)}.{tab}.err"), error);

    [When("I start the YouTube sync with my session")]
    public Task Sync() => auth.PostWithSession("/api/admin/tasks/youTubeSync/runs");

    [When("I start the full YouTube sync with my session")]
    public Task FullSync() => auth.PostWithSession("/api/admin/tasks/youTubeFullSync/runs");

    [When("I download everything of the channel {string} with my session")]
    public Task Everything(string channel) =>
        auth.PostWithSession($"/api/admin/tasks/youTubeFullSync/runs?target={Of(channel)}");

    [When("I request the failed downloads of the channel {string} with my session")]
    public Task FailedDownloads(string channel) =>
        auth.RequestWithSession($"/api/admin/channels/{Of(channel)}/failed-downloads");

    [When("I try the failed downloads of the channel {string} again with my session")]
    public Task Retry(string channel) =>
        auth.PostWithSession($"/api/admin/tasks/retryDownloads/runs?target={Of(channel)}");

    private static string Of(string channel) => FakeTubeArchivist.ChannelId(channel);
}
