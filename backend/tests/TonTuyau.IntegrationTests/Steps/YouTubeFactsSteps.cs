using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.Library;
using TonTuyau.Application.Store;
using TonTuyau.Application.YouTube;
using TonTuyau.Infrastructure.Accounts;
using TonTuyau.Infrastructure.Library;
using TonTuyau.Infrastructure.YouTube;
using TonTuyau.IntegrationTests.Support;

namespace TonTuyau.IntegrationTests.Steps;

/// <summary>
/// What TonTuyau knows of videos and channels on YouTube (1.5): since 2.0, their rows in the library (PostgreSQL),
/// written as a refresh writes them: the state (<c>video_states</c>, <c>channel_states</c>), and once refreshed, the
/// same columns the import filled. Videos and channels are named as in the fake TubeArchivist ("alpha-01", "alpha");
/// they're in the library once an import ran.
/// </summary>
[Binding]
public sealed class YouTubeFactsSteps(ScenarioState state, SyncSteps sync, AuthSteps auth)
{
    private static readonly DateTimeOffset Checked = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    [Given("the video {string} is unavailable in TubeArchivist")]
    public void UnavailableInTubeArchivist(string name) =>
        sync.Fake.Mutate(() => sync.Fake.Video(name).Active = false);

    [Given("TonTuyau found the video {string} gone from YouTube")]
    public Task VideoGone(string name) => SaveVideoAsync(
        name,
        row =>
        {
            row.YouTube.State = YouTubeState.Gone;
            row.YouTube.CheckedAt = Checked;
            row.YouTube.Reason = "Video unavailable. This video has been removed by the uploader";
        });

    [Given("TonTuyau found the channel {string} gone from YouTube")]
    public async Task ChannelGone(string name)
    {
        string channelId = FakeTubeArchivist.ChannelId(name);
        await using (AsyncServiceScope scope = state.Backend.Services.CreateAsyncScope())
        {
            TonTuyauDbContext db = scope.ServiceProvider.GetRequiredService<TonTuyauDbContext>();
            ChannelRow row = await db.Channels.SingleAsync(c => c.Id == channelId);
            row.YouTube.State = YouTubeState.Gone;
            row.YouTube.CheckedAt = Checked;
            row.YouTube.Reason = "This account has been terminated";
            await db.SaveChangesAsync();
        }

        await state.Backend.Services
            .GetRequiredService<IChannelRepository>()
            .ReindexAsync([channelId], default);
    }

    [Given("TonTuyau refreshed the video {string}: the title {string}, {int} views")]
    public Task Refreshed(string name, string title, int views) => SaveVideoAsync(
        name,
        row =>
        {
            row.YouTube.State = YouTubeState.OnYouTube;
            row.YouTube.CheckedAt = Checked;
            row.YouTube.RefreshedAt = Checked;
            row.Title = title;
            row.Description = "From YouTube";
            row.Tags = ["youtube"];
            row.Views = views;
            row.Likes = 12;
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
        Assert.Equal(expected, Name(video.YouTube.State));
    }

    /// <summary>The comments as kept, in YouTube's order: "text by author", answers marked "↳".</summary>
    [Then("PostgreSQL has the comments of the video {string}: {string}")]
    public async Task Comments(string name, string expected)
    {
        await using AsyncServiceScope scope = state.Backend.Services.CreateAsyncScope();
        TonTuyauDbContext db = scope.ServiceProvider.GetRequiredService<TonTuyauDbContext>();
        string videoId = FakeTubeArchivist.VideoId(name);
        List<VideoCommentRow> rows = await db.VideoComments
            .Where(c => c.VideoId == videoId)
            .OrderBy(c => c.Position)
            .ToListAsync();
        Assert.Equal(
            expected,
            string.Join(" | ", rows.Select(c => (c.ParentId is null ? "" : "↳") + c.Text + " by " + c.Author)));
        VideoRow? video = await db.Videos.FindAsync(videoId);
        Assert.NotNull(video?.CommentsReadAt);
    }

    [Then("PostgreSQL has no comments of the video {string}, never read")]
    public async Task NoComments(string name)
    {
        await using AsyncServiceScope scope = state.Backend.Services.CreateAsyncScope();
        TonTuyauDbContext db = scope.ServiceProvider.GetRequiredService<TonTuyauDbContext>();
        string videoId = FakeTubeArchivist.VideoId(name);
        Assert.False(await db.VideoComments.AnyAsync(c => c.VideoId == videoId));
        VideoRow? video = await db.Videos.FindAsync(videoId);
        Assert.Null(video?.CommentsReadAt);
    }

    [Then("PostgreSQL has the chapters of the video {string}: {string}")]
    public async Task Chapters(string name, string expected)
    {
        await using AsyncServiceScope scope = state.Backend.Services.CreateAsyncScope();
        VideoRow? video = await scope.ServiceProvider
            .GetRequiredService<TonTuyauDbContext>()
            .Videos
            .FindAsync(FakeTubeArchivist.VideoId(name));
        Assert.NotNull(video);
        IEnumerable<string> chapters = video.Chapters
            .Select(c => string.Create(CultureInfo.InvariantCulture, $"{c.Start}-{c.End} {c.Title}"));
        Assert.Equal(expected, string.Join(" | ", chapters));
    }

    [Then("PostgreSQL has the channel {string} as {string}")]
    public async Task ChannelIs(string name, string expected)
    {
        await using AsyncServiceScope scope = state.Backend.Services.CreateAsyncScope();
        ChannelRow? row = await scope.ServiceProvider
            .GetRequiredService<TonTuyauDbContext>()
            .Channels
            .SingleOrDefaultAsync(c => c.Id == FakeTubeArchivist.ChannelId(name));
        Assert.NotNull(row);
        Assert.Equal(expected, Name(row.YouTube.State));
    }

    [Then("PostgreSQL has the video {string} as {string}")]
    public async Task VideoIs(string name, string expected)
    {
        await using AsyncServiceScope scope = state.Backend.Services.CreateAsyncScope();
        VideoRow? row = await scope.ServiceProvider
            .GetRequiredService<TonTuyauDbContext>()
            .Videos
            .SingleOrDefaultAsync(v => v.Id == FakeTubeArchivist.VideoId(name));
        Assert.NotNull(row);
        Assert.Equal(expected, Name(row.YouTube.State));
    }

    private static string Name(YouTubeState? state) => state switch
    {
        YouTubeState.OnYouTube => "on YouTube",
        YouTubeState.RetryLater => "retry later",
        YouTubeState.Gone => "gone",
        YouTubeState.Private => "private",
        YouTubeState.Unlisted => "unlisted",
        _ => "not checked",
    };

    /// <summary>Sets a video's facts on its row (written by a first import), then its index copy again.</summary>
    private async Task SaveVideoAsync(string name, Action<VideoRow> facts)
    {
        string videoId = FakeTubeArchivist.VideoId(name);
        await using (AsyncServiceScope scope = state.Backend.Services.CreateAsyncScope())
        {
            TonTuyauDbContext db = scope.ServiceProvider.GetRequiredService<TonTuyauDbContext>();
            facts(await db.Videos.SingleAsync(v => v.Id == videoId));
            await db.SaveChangesAsync();
        }

        await state.Backend.Services
            .GetRequiredService<IVideoRepository>()
            .ReindexAsync([videoId], default);
    }
}
