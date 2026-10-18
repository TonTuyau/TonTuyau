using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.Library;
using TonTuyau.Application.Store;
using TonTuyau.Application.YouTube;
using TonTuyau.Infrastructure.Accounts;
using TonTuyau.Infrastructure.Library;
using TonTuyau.IntegrationTests.Support;

namespace TonTuyau.IntegrationTests.Steps;

/// <summary>The library's tables in PostgreSQL (2.0), through the stores the sync uses.</summary>
[Binding]
public sealed class LibraryStoreSteps(ScenarioState state)
{
    private const string DefaultChannel = "UCaaaaaaaaaaaaaaaaaaaaaa";

    private readonly Dictionary<string, Video> _videos = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Channel> _channels = new(StringComparer.Ordinal);
    private UpsertResult? _upsert;
    private int _deleted;

    private IServiceProvider Services => state.Backend.Services;

    private IVideoStore Videos => Services.GetRequiredService<IVideoStore>();

    private IChannelStore Channels => Services.GetRequiredService<IChannelStore>();

    [Given("the videos {string} are written to the library")]
    [When("the videos {string} are written to the library")]
    public async Task VideosAreWritten(string ids) =>
        _upsert = await Videos.UpsertAsync([.. Ids(ids).Select(id => VideoFor(id))], null, CancellationToken.None);

    [Given("the videos {string} of the channel {string} are written to the library")]
    public async Task ChannelVideosAreWritten(string ids, string channelId) =>
        _upsert = await Videos.UpsertAsync(
            [.. Ids(ids).Select(id => VideoFor(id, channelId))],
            null,
            CancellationToken.None);

    [When("the videos {string} are written to the library by run {string}")]
    public async Task VideosAreWrittenByRun(string ids, string run) =>
        _upsert = await Videos.UpsertAsync([.. Ids(ids).Select(id => VideoFor(id))], run, CancellationToken.None);

    [When("the library's video {string} gets the title {string} and the videos {string} are written to the library")]
    public async Task TitleChangesAndWrite(string id, string title, string ids)
    {
        _videos[id] = VideoFor(id) with { Title = title };
        await VideosAreWritten(ids);
    }

    [When("the library marks the videos {string} as seen by run {string}")]
    public Task MarkSeen(string ids, string run) => Videos.MarkSeenAsync(Ids(ids), run, CancellationToken.None);

    [When("the library flags the files of {string} as missing")]
    public Task FlagMissing(string ids) => Videos.SetMediaMissingAsync(Ids(ids), true, CancellationToken.None);

    [When("the library clears the file flag of {string}")]
    public Task ClearMissing(string ids) => Videos.SetMediaMissingAsync(Ids(ids), false, CancellationToken.None);

    [When("the library records the thumbnail of {string} as {int} px wide with the versions {string}")]
    public Task RecordThumbnail(string id, int width, string versions) =>
        Videos.SetThumbnailArtAsync(
            new Dictionary<string, ArtVersions> { [id] = new(width, Widths(versions)) },
            CancellationToken.None);

    [Given("TonTuyau refreshed the library's video {string} on YouTube with the title {string}")]
    public async Task RefreshedOnYouTube(string id, string title)
    {
        AsyncServiceScope scope = Services.CreateAsyncScope();
        await using (scope)
        {
            TonTuyauDbContext db = scope.ServiceProvider.GetRequiredService<TonTuyauDbContext>();
            VideoRow row = await db.Videos.SingleAsync(v => v.Id == id);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            row.YouTubeState = YouTubeState.OnYouTube;
            row.YouTubeCheckedAt = now;
            row.YouTubeRefreshedAt = now;
            row.YouTubeTitle = title;
            row.YouTubeChannelName = "Chaîne";
            row.YouTubeDescription = "";
            await db.SaveChangesAsync();
        }
    }

    [When("the videos {string} are deleted from the library")]
    public async Task VideosAreDeleted(string ids) =>
        _deleted = await Videos.DeleteAsync(Ids(ids), CancellationToken.None);

    [Then("the library counts {int} created, {int} updated and {int} unchanged")]
    public void UpsertCounts(int created, int updated, int unchanged) =>
        Assert.Equal((created, updated, unchanged), (_upsert!.Created, _upsert.Updated, _upsert.Unchanged));

    [Then("the library's video {string} is the one written, tracks, tags and duration included")]
    public async Task VideoIsTheOneWritten(string id)
    {
        Video stored = (await Videos.GetAsync(id, CancellationToken.None))!;
        Video written = VideoFor(id);
        // Never checked on YouTube: TubeArchivist's flag tells (active: not checked).
        Assert.Equal(
            written with { YouTube = YouTubeStatus.FromTubeArchivist(true) },
            stored with
            {
                Tags = written.Tags,
                Categories = written.Categories,
                PlaylistIds = written.PlaylistIds,
                Tracks = written.Tracks,
            });
        Assert.Equal(written.Tags, stored.Tags);
        Assert.Equal(written.Categories, stored.Categories);
        Assert.Equal(written.PlaylistIds, stored.PlaylistIds);
        Assert.Equal(written.Tracks, stored.Tracks);
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

    [Then("{int} video(s) was/were deleted from the library")]
    public void DeletedCount(int count) => Assert.Equal(count, _deleted);

    [Then("the library's videos not seen by run {string} are {string}")]
    public async Task NotSeen(string run, string ids)
    {
        IReadOnlyList<VideoSummary> notSeen = await Videos.FindNotSeenAsync(
            run,
            [DefaultChannel],
            CancellationToken.None);
        Assert.Equal(Ids(ids), notSeen.Select(v => v.Id).Order(StringComparer.Ordinal));
    }

    [Then("the library's files flagged as missing are {string}")]
    public async Task FlaggedMissing(string ids) =>
        Assert.Equal(
            Ids(ids),
            (await Videos.GetMediaMissingIdsAsync(CancellationToken.None)).Order(StringComparer.Ordinal));

    [Then("the library's video {string} has a thumbnail {int} px wide with the versions {string}")]
    public async Task HasThumbnailArt(string id, int width, string versions) =>
        Assert.Equal(
            new ArtVersions(width, Widths(versions)),
            (await Videos.GetStoredAsync(id, CancellationToken.None))?.ThumbnailArt);

    [Then("the library's videos of the channel {string} are {string}")]
    public async Task VideosOfChannel(string channelId, string ids)
    {
        IReadOnlyList<VideoSummary> found = await Videos.FindByChannelsAsync([channelId], CancellationToken.None);
        Assert.Equal(Ids(ids), found.Select(v => v.Id).Order(StringComparer.Ordinal));
    }

    [Given("the channels {string} are written to the library")]
    [When("the channels {string} are written to the library")]
    public async Task ChannelsAreWritten(string ids) =>
        await Channels.UpsertAsync([.. Ids(ids).Select(ChannelFor)], null, CancellationToken.None);

    [When("the channels {string} are written to the library by run {string}")]
    public async Task ChannelsAreWrittenByRun(string ids, string run) =>
        await Channels.UpsertAsync([.. Ids(ids).Select(ChannelFor)], run, CancellationToken.None);

    [When("the library records the {word} of the channel {string} as {int} px wide with the versions {string}")]
    public Task RecordChannelArt(string picture, string id, int width, string versions) =>
        Channels.SetArtAsync(
            picture == "banner" ? ArtSubject.ChannelBanner : ArtSubject.ChannelThumb,
            new Dictionary<string, ArtVersions> { [id] = new(width, Widths(versions)) },
            CancellationToken.None);

    [Then("the library's channel {string} is the one written, tabs and tags included")]
    public async Task ChannelIsTheOneWritten(string id)
    {
        Channel stored = (await Channels.GetAsync(id, CancellationToken.None))!;
        Channel written = ChannelFor(id);
        Assert.Equal(
            written with { YouTube = YouTubeStatus.FromTubeArchivist(true) },
            stored with { Tags = written.Tags, Tabs = written.Tabs });
        Assert.Equal(written.Tags, stored.Tags);
        Assert.Equal(written.Tabs, stored.Tabs);
    }

    [Then("the library's channel {string} has a {word} {int} px wide with the versions {string}")]
    public async Task ChannelHasArt(string id, string picture, int width, string versions)
    {
        StoredChannel stored = (await Channels.GetStoredAsync(id, CancellationToken.None))!;
        Assert.Equal(
            new ArtVersions(width, Widths(versions)),
            picture == "banner" ? stored.BannerArt : stored.ThumbArt);
    }

    [Then("the library's channels not seen by run {string} are {string}")]
    public async Task ChannelsNotSeen(string run, string ids)
    {
        IReadOnlyList<ChannelSummary> notSeen = await Channels.FindNotSeenAsync(run, CancellationToken.None);
        Assert.Equal(Ids(ids), notSeen.Select(c => c.Id).Order(StringComparer.Ordinal));
    }

    private Video VideoFor(string id, string channelId = DefaultChannel)
    {
        if (!_videos.TryGetValue(id, out Video? video))
        {
            video = new Video(
                id,
                channelId,
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
                $"{channelId}/{id}.mp4",
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
