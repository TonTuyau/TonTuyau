using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.Library;
using TonTuyau.Application.Store;
using TonTuyau.Application.YouTube;
using TonTuyau.Infrastructure.Accounts;
using TonTuyau.Infrastructure.Library;
using TonTuyau.IntegrationTests.Support;

namespace TonTuyau.IntegrationTests.Steps;

/// <summary>The library's tables in PostgreSQL (2.0), through their stores.</summary>
[Binding]
public sealed class LibraryStoreSteps(ScenarioState state)
{
    private const string DefaultChannel = "UCaaaaaaaaaaaaaaaaaaaaaa";

    private readonly Dictionary<string, Video> _videos = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Channel> _channels = new(StringComparer.Ordinal);
    private UpsertResult? _added;

    private static readonly YouTubeStatus NotChecked = new(YouTubeState.NotChecked, null, null, null);

    private IServiceProvider Services => state.Backend.Services;

    private IVideoStore Videos => Services.GetRequiredService<IVideoStore>();

    private IChannelStore Channels => Services.GetRequiredService<IChannelStore>();

    [Given("the videos {string} are added to the library")]
    [When("the videos {string} are added to the library")]
    public async Task VideosAreAdded(string ids) =>
        _added = await Videos.AddMissingAsync([.. Ids(ids).Select(id => VideoFor(id))], CancellationToken.None);

    [When("the library's video {string} gets the title {string} and the videos {string} are added to the library")]
    public async Task TitleChangesAndAdd(string id, string title, string ids)
    {
        _videos[id] = VideoFor(id) with { Title = title };
        await VideosAreAdded(ids);
    }

    [When("the video {string}, inactive in TubeArchivist, is added to the library")]
    public async Task InactiveVideoIsAdded(string id)
    {
        _videos[id] = VideoFor(id) with { Active = false };
        await VideosAreAdded(id);
    }

    [When("the library flags the files of {string} as missing")]
    public Task FlagMissing(string ids) => Videos.SetMediaMissingAsync(Ids(ids), true, CancellationToken.None);

    [When("the library clears the file flag of {string}")]
    public Task ClearMissing(string ids) => Videos.SetMediaMissingAsync(Ids(ids), false, CancellationToken.None);

    [When("the library records the thumbnail of {string} as {int} px wide with the versions {string}")]
    public Task RecordThumbnail(string id, int width, string versions) =>
        Videos.SetThumbnailArtAsync(
            new Dictionary<string, ArtVersions> { [id] = new(width, Widths(versions)) },
            CancellationToken.None);

    /// <summary>What a refresh writes (A2 gives it its own write): the same columns, the state, its date.</summary>
    [Given("TonTuyau refreshed the library's video {string} on YouTube with the title {string}")]
    public async Task RefreshedOnYouTube(string id, string title)
    {
        AsyncServiceScope scope = Services.CreateAsyncScope();
        await using (scope)
        {
            TonTuyauDbContext db = scope.ServiceProvider.GetRequiredService<TonTuyauDbContext>();
            VideoRow row = await db.Videos.SingleAsync(v => v.Id == id);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            row.Title = title;
            row.YouTube.State = YouTubeState.OnYouTube;
            row.YouTube.CheckedAt = now;
            row.YouTube.RefreshedAt = now;
            await db.SaveChangesAsync();
        }
    }

    [Then("the library counts {int} added and {int} already there")]
    public void AddCounts(int added, int already) =>
        Assert.Equal((added, 0, already), (_added!.Created, _added.Updated, _added.Unchanged));

    [Then("the library's video {string} is the one added, tracks, tags and duration included")]
    public async Task VideoIsTheOneAdded(string id)
    {
        Video stored = (await Videos.GetAsync(id, CancellationToken.None))!;
        Video added = VideoFor(id);
        Assert.Equal(
            added with { YouTube = NotChecked },
            stored with
            {
                Tags = added.Tags,
                Categories = added.Categories,
                PlaylistIds = added.PlaylistIds,
                Tracks = added.Tracks,
            });
        Assert.Equal(added.Tags, stored.Tags);
        Assert.Equal(added.Categories, stored.Categories);
        Assert.Equal(added.PlaylistIds, stored.PlaylistIds);
        Assert.Equal(added.Tracks, stored.Tracks);
    }

    [Then("the library's video {string} is not checked on YouTube, and active")]
    public async Task VideoIsNotChecked(string id)
    {
        Video stored = (await Videos.GetAsync(id, CancellationToken.None))!;
        Assert.Equal(NotChecked, stored.YouTube);
        Assert.True(stored.Active);
        Assert.False(stored.GoneFromYouTube);
    }

    [Then("the library's video {string} has the title {string}")]
    public async Task VideoHasTitle(string id, string title) =>
        Assert.Equal(title, (await Videos.GetAsync(id, CancellationToken.None))?.Title);

    [Then("the library's video {string} is on YouTube, refreshed")]
    public async Task VideoIsRefreshed(string id)
    {
        YouTubeStatus? status = (await Videos.GetAsync(id, CancellationToken.None))?.YouTube;
        Assert.Equal(YouTubeState.OnYouTube, status?.State);
        Assert.NotNull(status?.RefreshedAt);
    }

    [Then("the library holds {int} video(s)")]
    public async Task LibraryHoldsVideos(int count) =>
        Assert.Equal(count, await Videos.CountAsync(CancellationToken.None));

    [Then("the library holds {int} channel(s)")]
    public async Task LibraryHoldsChannels(int count) =>
        Assert.Equal(count, await Channels.CountAsync(CancellationToken.None));

    [Then("the library's video {string} has its file flagged as missing")]
    public async Task FileFlagged(string id) =>
        Assert.True((await Videos.GetStoredAsync(id, CancellationToken.None))?.MediaMissing);

    [Then("the library's video {string} has its file")]
    public async Task FileThere(string id) =>
        Assert.False((await Videos.GetStoredAsync(id, CancellationToken.None))?.MediaMissing);

    [Then("the library's video {string} has a thumbnail {int} px wide with the versions {string}")]
    public async Task HasThumbnailArt(string id, int width, string versions) =>
        Assert.Equal(
            new ArtVersions(width, Widths(versions)),
            (await Videos.GetStoredAsync(id, CancellationToken.None))?.ThumbnailArt);

    [Then("the library keeps {int} video states and {int} channel state(s), all not checked")]
    public async Task StatesInTheirTables(int videos, int channels)
    {
        AsyncServiceScope scope = Services.CreateAsyncScope();
        await using (scope)
        {
            TonTuyauDbContext db = scope.ServiceProvider.GetRequiredService<TonTuyauDbContext>();
            List<YouTubeState> videoStates = await db.VideoStates
                .Select(s => s.State)
                .ToListAsync();
            List<YouTubeState> channelStates = await db.ChannelStates
                .Select(s => s.State)
                .ToListAsync();
            Assert.Equal((videos, channels), (videoStates.Count, channelStates.Count));
            Assert.All([.. videoStates, .. channelStates], state => Assert.Equal(YouTubeState.NotChecked, state));
        }
    }

    [Given("the channels {string} are added to the library")]
    [When("the channels {string} are added to the library")]
    public async Task ChannelsAreAdded(string ids) =>
        _added = await Channels.AddMissingAsync([.. Ids(ids).Select(ChannelFor)], CancellationToken.None);

    [When("the channel {string} gets the name {string} and the channels {string} are added to the library")]
    public async Task NameChangesAndAdd(string id, string name, string ids)
    {
        _channels[id] = ChannelFor(id) with { Name = name };
        await ChannelsAreAdded(ids);
    }

    [When("the library records the {word} of the channel {string} as {int} px wide with the versions {string}")]
    public Task RecordChannelArt(string picture, string id, int width, string versions) =>
        Channels.SetArtAsync(
            picture == "banner" ? ArtSubject.ChannelBanner : ArtSubject.ChannelThumb,
            new Dictionary<string, ArtVersions> { [id] = new(width, Widths(versions)) },
            CancellationToken.None);

    [Then("the library's channel {string} is the one added, tabs and tags included")]
    public async Task ChannelIsTheOneAdded(string id)
    {
        Channel stored = (await Channels.GetAsync(id, CancellationToken.None))!;
        Channel added = ChannelFor(id);
        Assert.Equal(added with { YouTube = NotChecked }, stored with { Tags = added.Tags, Tabs = added.Tabs });
        Assert.Equal(added.Tags, stored.Tags);
        Assert.Equal(added.Tabs, stored.Tabs);
    }

    [Then("the library's channel {string} has the name {string}")]
    public async Task ChannelHasName(string id, string name) =>
        Assert.Equal(name, (await Channels.GetAsync(id, CancellationToken.None))?.Name);

    [Then("the library's channel {string} has a {word} {int} px wide with the versions {string}")]
    public async Task ChannelHasArt(string id, string picture, int width, string versions)
    {
        StoredChannel stored = (await Channels.GetStoredAsync(id, CancellationToken.None))!;
        Assert.Equal(
            new ArtVersions(width, Widths(versions)),
            picture == "banner" ? stored.BannerArt : stored.ThumbArt);
    }

    private Video VideoFor(string id)
    {
        if (!_videos.TryGetValue(id, out Video? video))
        {
            video = new Video(
                id,
                DefaultChannel,
                "Chaîne",
                $"Vidéo {id}",
                "Une description.",
                VideoType.Short,
                true,
                new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 3, 2, 10, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 3, 3, 10, 0, 0, TimeSpan.Zero),
                TimeSpan.FromSeconds(61.5),
                123_456,
                $"{DefaultChannel}/{id}.mp4",
                false,
                $"/cache/videos/{id[0]}/{id}.jpg",
                ["pompe", "tuyau"],
                ["Science"],
                ["PLaaaa"],
                new VideoStats(10, 2, 1),
                [
                    new MediaTrack(MediaTrackKind.Video, "vp9", 1_000_000, 1920, 1080),
                    new MediaTrack(MediaTrackKind.Audio, "opus", 128_000, null, null),
                ]);
            _videos[id] = video;
        }

        return video;
    }

    private Channel ChannelFor(string id)
    {
        if (!_channels.TryGetValue(id, out Channel? channel))
        {
            channel = new Channel(
                id,
                $"Chaîne {id}",
                "Sa description.",
                4_200,
                true,
                new DateTimeOffset(2026, 3, 3, 10, 0, 0, TimeSpan.Zero),
                ["pompe"],
                [VideoType.Video, VideoType.Short],
                $"/cache/channels/{id}_thumb.jpg",
                $"/cache/channels/{id}_banner.jpg",
                null);
            _channels[id] = channel;
        }

        return channel;
    }

    private static string[] Ids(string ids) =>
        ids
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static int[] Widths(string widths) =>
        widths
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(int.Parse)
            .ToArray();
}
