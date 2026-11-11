using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.Configuration;
using TonTuyau.Infrastructure.Elasticsearch;
using TonTuyau.IntegrationTests.Support;

namespace TonTuyau.IntegrationTests.Steps;

/// <summary>
/// The purge (2.3.0): videos of a channel deleted from TonTuyau, their files with them; those picked refreshed or
/// downloaded again; and "Download everything" of a channel with its download settings this once.
/// </summary>
[Binding]
public sealed class PurgeSteps(ScenarioState state, AuthSteps auth)
{
    private string MediaRoot => state.Backend.Services.GetRequiredService<TonTuyauOptions>().Storage.MediaRoot;

    [When("I purge the channel {string} with my session")]
    public Task PurgeChannel(string channel) =>
        auth.PostWithSession($"/api/admin/tasks/purgeVideos/runs?target={FakeTubeArchivist.ChannelId(channel)}");

    [When("I purge the videos {string} of the channel {string} with my session")]
    public Task PurgeVideos(string names, string channel) => AskVideos("purgeVideos", names, channel, "");

    [When("I ask {string} for the videos {string} of the channel {string} with my session")]
    public Task AskVideos(string task, string names, string channel) => AskVideos(task, names, channel, "");

    [When("I ask {string} for the videos {string} of the channel {string} with {string} with my session")]
    public Task AskVideos(string task, string names, string channel, string query)
    {
        string ids = string.Join(
            ',',
            names.Split(',', StringSplitOptions.TrimEntries).Select(FakeTubeArchivist.VideoId));
        return auth.PostWithSession(
            $"/api/admin/tasks/{task}/runs?target={FakeTubeArchivist.ChannelId(channel)}&videos={ids}&{query}");
    }

    [When("I download everything of the channel {string} with {string} with my session")]
    public Task Everything(string channel, string query) =>
        auth.PostWithSession(
            $"/api/admin/tasks/youTubeFullSync/runs?target={FakeTubeArchivist.ChannelId(channel)}&{query}");

    [Then("the media folder doesn't have the file of the video {string}")]
    public void FileGone(string name)
    {
        string file = Path.Combine(
            MediaRoot,
            FakeTubeArchivist.ChannelId(name.Split('-')[0]),
            FakeTubeArchivist.VideoId(name) + ".mkv");
        Assert.False(File.Exists(file), $"{file} is still there");
    }

    [Then("the index doesn't have the video {string}")]
    public async Task NotIndexed(string name) =>
        Assert.Null(await state.Backend.Services
            .GetRequiredService<VideoIndex>()
            .GetStoredAsync(FakeTubeArchivist.VideoId(name), CancellationToken.None));
}
