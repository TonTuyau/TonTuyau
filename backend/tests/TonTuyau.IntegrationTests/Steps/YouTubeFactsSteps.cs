using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.Library;
using TonTuyau.Application.Store;
using TonTuyau.Application.YouTube;
using TonTuyau.Infrastructure.Accounts;
using TonTuyau.Infrastructure.YouTube;
using TonTuyau.IntegrationTests.Support;

namespace TonTuyau.IntegrationTests.Steps;

/// <summary>
/// TonTuyau's own facts about videos and channels (1.5), in PostgreSQL, laid by the sync over what TubeArchivist says.
/// Videos and channels are named as in the fake TubeArchivist ("alpha-01", "alpha").
/// </summary>
[Binding]
public sealed class YouTubeFactsSteps(ScenarioState state, SyncSteps sync, AuthSteps auth)
{
    private static readonly DateTimeOffset Checked = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    [Given("the video {string} is unavailable in TubeArchivist")]
    public void UnavailableInTubeArchivist(string name) =>
        sync.Fake.Mutate(() => sync.Fake.Video(name).Active = false);

    [Given("TonTuyau found the video {string} gone from YouTube")]
    public Task VideoGone(string name) => SaveAsync(new YouTubeVideoRow
    {
        VideoId = FakeTubeArchivist.VideoId(name),
        State = YouTubeState.Gone,
        CheckedAt = Checked,
        Reason = "Video unavailable. This video has been removed by the uploader",
    });

    [Given("TonTuyau found the channel {string} gone from YouTube")]
    public Task ChannelGone(string name) => SaveAsync(new YouTubeChannelRow
    {
        ChannelId = FakeTubeArchivist.ChannelId(name),
        State = YouTubeState.Gone,
        CheckedAt = Checked,
        Reason = "This account has been terminated",
    });

    [Given("TonTuyau refreshed the video {string}: the title {string}, {int} views")]
    public Task Refreshed(string name, string title, int views) => SaveAsync(new YouTubeVideoRow
    {
        VideoId = FakeTubeArchivist.VideoId(name),
        State = YouTubeState.OnYouTube,
        CheckedAt = Checked,
        RefreshedAt = Checked,
        Title = title,
        Description = "From YouTube",
        Tags = ["youtube"],
        Categories = ["Education"],
        Views = views,
        Likes = 12,
    });

    [When("I request the video {string} of TubeArchivist")]
    public Task RequestVideo(string name) =>
        auth.RequestWithSession($"/api/videos/{FakeTubeArchivist.VideoId(name)}");

    [When("I request the channel {string} of TubeArchivist")]
    public Task RequestChannel(string name) =>
        auth.RequestWithSession($"/api/channels/{FakeTubeArchivist.ChannelId(name)}");

    [Then("the stored YouTube state of the video {string} is {string}")]
    public async Task StoredState(string name, string expected)
    {
        Video? video = await state.Backend.Services
            .GetRequiredService<IVideoRepository>()
            .GetAsync(FakeTubeArchivist.VideoId(name), default);
        Assert.NotNull(video);
        Assert.Equal(expected, Name(video.CurrentYouTube.State));
    }

    private static string Name(YouTubeState state) => state switch
    {
        YouTubeState.OnYouTube => "on YouTube",
        YouTubeState.RetryLater => "retry later",
        YouTubeState.Gone => "gone",
        _ => "not checked",
    };

    /// <summary>Written once the backend made its tables (it migrates PostgreSQL at startup).</summary>
    private async Task SaveAsync(object row)
    {
        _ = state.Backend.Services;
        await using AsyncServiceScope scope = state.Backend.Services.CreateAsyncScope();
        TonTuyauDbContext db = scope.ServiceProvider.GetRequiredService<TonTuyauDbContext>();
        db.Add(row);
        await db.SaveChangesAsync();
    }
}
