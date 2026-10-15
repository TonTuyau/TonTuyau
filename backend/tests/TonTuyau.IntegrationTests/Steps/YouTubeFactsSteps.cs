using System.Globalization;
using Microsoft.EntityFrameworkCore;
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
        ChannelId = ChannelOf(name),
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
        ChannelId = ChannelOf(name),
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
        YouTubeVideoRow? video = await db.YouTubeVideos.FindAsync(videoId);
        Assert.NotNull(video?.CommentsReadAt);
    }

    [Then("PostgreSQL has no comments of the video {string}, never read")]
    public async Task NoComments(string name)
    {
        await using AsyncServiceScope scope = state.Backend.Services.CreateAsyncScope();
        TonTuyauDbContext db = scope.ServiceProvider.GetRequiredService<TonTuyauDbContext>();
        string videoId = FakeTubeArchivist.VideoId(name);
        Assert.False(await db.VideoComments.AnyAsync(c => c.VideoId == videoId));
        YouTubeVideoRow? video = await db.YouTubeVideos.FindAsync(videoId);
        Assert.Null(video?.CommentsReadAt);
    }

    [Then("PostgreSQL has the chapters of the video {string}: {string}")]
    public async Task Chapters(string name, string expected)
    {
        await using AsyncServiceScope scope = state.Backend.Services.CreateAsyncScope();
        YouTubeVideoRow? video = await scope.ServiceProvider
            .GetRequiredService<TonTuyauDbContext>()
            .YouTubeVideos
            .FindAsync(FakeTubeArchivist.VideoId(name));
        Assert.NotNull(video);
        IEnumerable<string> chapters = video.Chapters
            .Select(c => string.Create(CultureInfo.InvariantCulture, $"{c.Start}-{c.End} {c.Title}"));
        Assert.Equal(expected, string.Join(" | ", chapters));
    }

    [Then("PostgreSQL has the channel {string} as {string}")]
    public async Task ChannelRow(string name, string expected)
    {
        await using AsyncServiceScope scope = state.Backend.Services.CreateAsyncScope();
        YouTubeChannelRow? row = await scope.ServiceProvider
            .GetRequiredService<TonTuyauDbContext>()
            .YouTubeChannels
            .FindAsync(FakeTubeArchivist.ChannelId(name));
        Assert.NotNull(row);
        Assert.Equal(expected, Name(row.State));
    }

    private static string Name(YouTubeState state) => state switch
    {
        YouTubeState.OnYouTube => "on YouTube",
        YouTubeState.RetryLater => "retry later",
        YouTubeState.Gone => "gone",
        _ => "not checked",
    };

    /// <summary>"alpha-01" → the channel "alpha".</summary>
    private static string ChannelOf(string video) => FakeTubeArchivist.ChannelId(video.Split('-')[0]);

    /// <summary>
    /// Written once the backend made its tables (it migrates PostgreSQL at startup). A video's channel gets a row first
    /// (the foreign key), "not checked" unless a scenario said more; a channel updates that row.
    /// </summary>
    private async Task SaveAsync(object row)
    {
        await using AsyncServiceScope scope = state.Backend.Services.CreateAsyncScope();
        TonTuyauDbContext db = scope.ServiceProvider.GetRequiredService<TonTuyauDbContext>();
        if (row is YouTubeVideoRow video && await db.YouTubeChannels.FindAsync(video.ChannelId) is null)
        {
            db.YouTubeChannels.Add(new YouTubeChannelRow { ChannelId = video.ChannelId });
        }

        if (row is YouTubeChannelRow channel && await db.YouTubeChannels.FindAsync(channel.ChannelId) is { } known)
        {
            // Updated, not replaced: deleting it would delete its videos' rows too.
            db.Entry(known).CurrentValues.SetValues(channel);
        }
        else
        {
            db.Add(row);
        }

        await db.SaveChangesAsync();
    }
}
