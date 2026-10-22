using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.Configuration;
using TonTuyau.Application.Library;
using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;
using TonTuyau.Infrastructure.Accounts;
using TonTuyau.IntegrationTests.Support;

namespace TonTuyau.IntegrationTests.Steps;

[Binding]
public sealed class SyncSteps(ScenarioState state) : IAsyncDisposable
{
    private readonly string _root = state.Temporary(Path.Combine(
        Path.GetTempPath(),
        "tontuyau-sync-" + Guid.NewGuid().ToString("N")[..8]));
    private readonly Dictionary<string, FakeVideo> _removed = new(StringComparer.Ordinal);
    private FakeTubeArchivist? _fake;
    private SyncRun? _run;
    private readonly CancellationTokenSource _caller = new();
    private int _listRequestsBefore;
    private int _runsBeforeRestart;
    private bool _countedBeforeRestart;
    private ISyncLease? _otherProcessLease;
    private Task _lettingGo = Task.CompletedTask;

    public FakeTubeArchivist Fake => _fake!;

    private IServiceProvider Services => state.Backend.Services;

    private SyncService Sync => Services.GetRequiredService<SyncService>();

    private IVideoRepository Videos => Services.GetRequiredService<IVideoRepository>();

    private IChannelRepository Channels => Services.GetRequiredService<IChannelRepository>();

    private string ArtRoot => Path.Combine(_root, "art");

    private string MediaRoot => Path.Combine(_root, "media");

    [BeforeScenario("sync")]
    public async Task StartFakeTubeArchivist()
    {
        _fake = await FakeTubeArchivist.StartAsync();
        Set(ConfigKeys.TaUrl, Fake.Url);
        Set(ConfigKeys.ArtRoot, ArtRoot);
        Set(ConfigKeys.MediaRoot, MediaRoot);
    }

    // ---------------------------------------------------------------- TubeArchivist's library

    [Given("TubeArchivist has the channel {string} with {int} videos")]
    public void ChannelWithVideos(string name, int count) => Fake.Mutate(() =>
    {
        Fake.Channels.Add(new FakeChannel(FakeTubeArchivist.ChannelId(name), name));
        for (int i = 1; i <= count; i++)
        {
            AddVideo($"{name}-{i:00}", name, NextDownload());
        }
    });

    [Given("TubeArchivist downloads {int} new videos in {string}")]
    public void DownloadNew(int count, string channel) => Fake.Mutate(() =>
    {
        for (int i = 1; i <= count; i++)
        {
            AddVideo(NewName(channel), channel, NextDownload());
        }
    });

    [Given("TubeArchivist downloads {int} new short(s) and {int} new stream(s) in {string}")]
    public void DownloadShortsAndStreams(int shorts, int streams, string channel) => Fake.Mutate(() =>
    {
        foreach (string type in Enumerable.Repeat("shorts", shorts).Concat(Enumerable.Repeat("streams", streams)))
        {
            string name = NewName(channel);
            AddVideo(name, channel, NextDownload());
            Fake.Video(name).Type = type;
        }
    });

    [Given("TubeArchivist downloads {int} new videos in {string}, all in the same second")]
    public void DownloadSameSecond(int count, string channel) => Fake.Mutate(() =>
    {
        long second = NextDownload();
        for (int i = 1; i <= count; i++)
        {
            AddVideo(NewName(channel), channel, second);
        }
    });

    [Given("TubeArchivist imports the video {string} in {string}, downloaded {int} days before the first one")]
    public void ImportOld(string name, string channel, int days) =>
        Fake.Mutate(() => AddVideo(name, channel, FakeTubeArchivist.Epoch.AddDays(-days).ToUnixTimeSeconds()));

    [Given("TubeArchivist downloads the video {string} in {string} right before answering page {int}")]
    public void DownloadWhilePaging(string name, string channel, int page) =>
        Fake.BeforeAnswer[$"page={page.ToString(CultureInfo.InvariantCulture)}"] = () => AddVideo(
            name,
            channel,
            NextDownload());

    /// <summary>As the CLI's Ctrl-C (or, in 1.4, the Tasks page) does: the run's caller asks it to stop.</summary>
    [Given("the caller cancels the import right before TubeArchivist answers page {int}")]
    public void CancelWhilePaging(int page) =>
        Fake.BeforeAnswer[$"page={page.ToString(CultureInfo.InvariantCulture)}"] = () => _caller.Cancel();

    [Given("TubeArchivist deletes {int} videos of {string}")]
    public void DeleteMany(int count, string channel) => Fake.Mutate(() =>
    {
        FakeVideo[] victims = Fake.Videos
            .Where(v => v.ChannelId == FakeTubeArchivist.ChannelId(channel))
            .OrderByDescending(v => v.Id, StringComparer.Ordinal)
            .Take(count)
            .ToArray();
        foreach (FakeVideo? video in victims)
        {
            Remove(video);
        }
    });

    [Given("TubeArchivist deletes the video {string}")]
    public void DeleteOne(string name) => Fake.Mutate(() =>
    {
        Remove(Fake.Video(name));
    });

    [Given("TubeArchivist deletes the channel {string} with its videos")]
    public void DeleteChannel(string name) => Fake.Mutate(() =>
    {
        string id = FakeTubeArchivist.ChannelId(name);
        foreach (FakeVideo? video in Fake.Videos.Where(v => v.ChannelId == id).ToArray())
        {
            Remove(video);
        }

        Fake.Channels.RemoveAll(c => c.Id == id);
    });

    [Given("TubeArchivist gets the video {string} back")]
    public void GetBack(string name) => Fake.Mutate(() => Fake.Videos.Add(_removed[FakeTubeArchivist.VideoId(name)]));

    [Given("the video {string} gets the title {string}")]
    public void Retitle(string name, string title) => Fake.Mutate(() => Fake.Video(name).Title = title);

    [Given("the video {string} becomes a stream")]
    public void BecomesStream(string name) => Fake.Mutate(() => Fake.Video(name).Type = "streams");

    [Given("the video {string} is broken in TubeArchivist")]
    public void Broken(string name) => Fake.Mutate(() => Fake.Video(name).Broken = true);

    [Given("every video of {string} is broken in TubeArchivist")]
    public void AllBroken(string channel) => Fake.Mutate(() =>
    {
        foreach (FakeVideo? video in Fake.Videos.Where(v => v.ChannelId == FakeTubeArchivist.ChannelId(channel)))
        {
            video.Broken = true;
        }
    });

    [Given("the video {string} has its file path in the folder of {string}")]
    public void WrongFolder(string name, string channel) =>
        Fake.Mutate(() => Fake.Video(name).MediaFolder = FakeTubeArchivist.ChannelId(channel));

    [Given("TubeArchivist's list of {string} counts {string} but doesn't return it")]
    public void Dropped(string channel, string name) => Fake.Mutate(() => Fake.Video(name).DroppedFromPages = true);

    [Given("TubeArchivist's lists don't show {string} any more, but TubeArchivist still has it")]
    public void Hidden(string name) => Fake.Mutate(() => Fake.Video(name).HiddenFromLists = true);

    [Given("TubeArchivist has no thumbnail for {string}")]
    public void NoThumbnail(string name) => Fake.MissingArt.Add(ThumbnailOf(FakeTubeArchivist.VideoId(name)));

    [When("TubeArchivist gets the thumbnail of {string} back")]
    public void ThumbnailBack(string name) => Fake.MissingArt.Remove(ThumbnailOf(FakeTubeArchivist.VideoId(name)));

    [Given("the thumbnail of {string} is {int} px wide in TubeArchivist")]
    public void ThumbnailWidth(string name, int width) =>
        Fake.ArtSizes[ThumbnailOf(FakeTubeArchivist.VideoId(name))] = (width, Math.Max(1, width * 9 / 16));

    [Given("the thumbnail of {string} isn't an image in TubeArchivist")]
    public void ThumbnailBroken(string name) => Fake.BrokenArt.Add(ThumbnailOf(FakeTubeArchivist.VideoId(name)));

    [Given("TubeArchivist fails with {int} on {string} {int} times")]
    public void Fails(int status, string pattern, int times) => Fake.Failures[pattern] = (status, times);

    [Given("TubeArchivist answers {int} to everything")]
    public void Down(int status) => Fake.DownWithStatus = status;

    [When("TubeArchivist works again")]
    public void WorksAgain()
    {
        Fake.Failures.Clear();
        Fake.DownWithStatus = null;
    }

    // ---------------------------------------------------------------- TonTuyau settings and environment

    [Given("TubeArchivist's result window is {int}")]
    public void Window(int window)
    {
        Fake.Window = window;
        Restart(ConfigKeys.TaResultWindow, window);
    }

    // ---------------------------------------------------------------- automatic index rebuild (QA10 M5-10)

    /// <summary>The backend imports TubeArchivist's library when it starts, as in production (2.0).</summary>
    [Given("the backend imports from TubeArchivist when it starts")]
    public async Task ImportsAtStartup()
    {
        // Counted now, with the current backend: reading them later would start a backend with the import on before
        // the restart the scenario is about.
        _runsBeforeRestart = (await RecentRuns()).Count;
        _countedBeforeRestart = true;
        state.ImportsAtStartup = true;
        state.StopBackend();
    }

    [When("the backend restarts and is ready")]
    public async Task BackendRestarts()
    {
        if (!_countedBeforeRestart)
        {
            _runsBeforeRestart = (await RecentRuns()).Count;
        }

        state.StopBackend();
        await state.WaitUntilReadyAsync();
    }

    [Then("an import started by the backend succeeds, with {int} new videos")]
    public async Task StartupImportSucceeds(int created)
    {
        SyncRun run = await StartupRunAsync(SyncMode.Import);
        Assert.True(run.Outcome == SyncOutcome.Succeeded, Describe());
        Assert.Equal(created, run.Counts.Created);
    }

    [Then("an index rebuild started by the backend succeeds")]
    public async Task StartupRebuildSucceeds()
    {
        SyncRun run = await StartupRunAsync(SyncMode.IndexRebuild);
        Assert.Equal(SyncOutcome.Succeeded, run.Outcome);
        Assert.True(run.IndexRebuild);
    }

    [Then("an index rebuild started by the backend fails")]
    public async Task StartupRebuildFails()
    {
        SyncRun run = await StartupRunAsync(SyncMode.IndexRebuild);
        Assert.Equal(SyncOutcome.Failed, run.Outcome);
        Assert.True(run.IndexRebuild);
    }

    [Then("no run starts by itself")]
    public async Task NoRunStarts()
    {
        // The startup check runs right after the host starts: give it time to do something if it were going to.
        await Task.Delay(TimeSpan.FromSeconds(3));
        Assert.Equal(_runsBeforeRestart, (await RecentRuns()).Count);
        Assert.Null(await Services.GetRequiredService<ISyncStateStore>().GetLockAsync(CancellationToken.None));
    }

    [Then("the run rebuilt the indices first")]
    public void RebuiltFirst()
    {
        Assert.True(_run!.IndexRebuild);
        Assert.Equal(SyncMode.Import, _run.Mode);
        Assert.Contains(_run.Notes, n => n.StartsWith("Index rebuild first", StringComparison.Ordinal));
    }

    [Then("the run didn't rebuild the indices")]
    public void NotRebuilt() => Assert.False(_run!.IndexRebuild);

    [Given("the media share has every video's file except {string}")]
    public void MediaShare(string missing)
    {
        foreach (FakeVideo? video in Fake.Videos.Where(v => v.Id != FakeTubeArchivist.VideoId(missing)))
        {
            WriteMediaFile(video);
        }
    }

    [When("the file of {string} appears on the share")]
    public void FileAppears(string name) => WriteMediaFile(Fake.Video(name));

    /// <summary>TubeArchivist's settings removed (2.0: TonTuyau runs without it); the backend restarts with that.</summary>
    [Given("TubeArchivist isn't set up")]
    public void NoTubeArchivist()
    {
        state.Settings.Set(ConfigKeys.EnvironmentName(ConfigKeys.TaUrl), null);
        state.Settings.Set(ConfigKeys.EnvironmentName(ConfigKeys.TaToken), null);
        state.StopBackend();
    }

    [Given("another process holds the run lock")]
    public async Task OtherProcessHoldsLock() =>
        _otherProcessLease = await Services
            .GetRequiredService<ISyncStateStore>()
            .TryAcquireLockAsync(SyncMode.Import, SyncTrigger.Cli, CancellationToken.None);

    /// <summary>The other process's run ends meanwhile (while the scenario waits on something else).</summary>
    [Given("the other process lets go of the run lock in {int} seconds")]
    public void OtherProcessLetsGo(int seconds)
    {
        ISyncLease lease = _otherProcessLease!;
        _otherProcessLease = null;
        _lettingGo = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds));
            await lease.DisposeAsync();
        });
    }

    // ---------------------------------------------------------------- runs

    [Given("a first import has run")]
    public async Task FirstImport()
    {
        await RunAsync(() => Sync.ImportAsync(SyncTrigger.Cli, null, NoSyncProgress.Instance, CancellationToken.None));
        Assert.True(_run!.Outcome == SyncOutcome.Succeeded, $"The first import failed: {_run.Error}");
    }

    [Given("another import has run")]
    [When("an import runs")]
    public Task Import() =>
        RunAsync(() => Sync.ImportAsync(SyncTrigger.Cli, null, NoSyncProgress.Instance, _caller.Token));

    [When("an index rebuild runs")]
    public Task Rebuild() => RunAsync(() => Sync.RebuildAsync(SyncTrigger.Cli, CancellationToken.None));

    // ---------------------------------------------------------------- outcomes

    [Then("the run succeeded")]
    public void Succeeded() => Assert.True(_run?.Outcome == SyncOutcome.Succeeded, Describe());

    /// <summary>
    /// From then on PostgreSQL refuses any video written (a trigger raising an error): a database failure in the
    /// middle of a run, at a known point.
    /// </summary>
    [Given("PostgreSQL refuses the videos written after TubeArchivist answers {string}")]
    public void RefuseVideosAfter(string request) =>
        Fake.BeforeAnswer[request] = () =>
        {
            using IServiceScope scope = Services.CreateScope();
            scope.ServiceProvider
                .GetRequiredService<TonTuyauDbContext>()
                .Database
                .ExecuteSqlRaw(
                    """
                    CREATE FUNCTION refuse_videos() RETURNS trigger LANGUAGE plpgsql AS
                      $$ BEGIN RAISE EXCEPTION 'refused by the test'; END $$;
                    CREATE TRIGGER refuse_videos BEFORE INSERT OR UPDATE ON videos
                      FOR EACH ROW EXECUTE FUNCTION refuse_videos();
                    """);
        };

    [Then("the run failed")]
    public void Failed() => Assert.True(_run?.Outcome == SyncOutcome.Failed, Describe());

    [Then("the run was canceled, not failed")]
    public void Canceled()
    {
        Assert.True(_run?.Outcome == SyncOutcome.Canceled, Describe());
        Assert.Null(_run.Error);
        Assert.NotNull(_run.FinishedAt);
    }

    [Then("the stored run is {word} too")]
    public async Task StoredOutcome(string outcome)
    {
        SyncRun? stored = await Services
            .GetRequiredService<ISyncStateStore>()
            .GetRunAsync(_run!.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(Enum.Parse<SyncOutcome>(outcome, ignoreCase: true), stored.Outcome);
        Assert.NotNull(stored.FinishedAt);
    }

    [Then("the run lock is free")]
    public async Task LockFree() =>
        Assert.Null(await Services.GetRequiredService<ISyncStateStore>().GetLockAsync(CancellationToken.None));

    [Then("the run failed because {string}")]
    public void FailedBecause(string reason)
    {
        Failed();
        Assert.Contains(reason, _run!.Error, StringComparison.Ordinal);
    }

    [Then("it is refused because {string}")]
    public void Refused(string reason)
    {
        Assert.NotNull(state.Error);
        Assert.Contains(reason, state.Error.Message, StringComparison.Ordinal);
    }

    [Then("the run notes mention {string}")]
    public void NotesMention(string text) =>
        Assert.True(_run!.Notes.Any(n => n.Contains(text, StringComparison.Ordinal)), Describe());

    [Then("a run note has a line starting with {string}")]
    public void NoteLine(string start) =>
        Assert.True(
            _run!.Notes
                .SelectMany(n => n.ReplaceLineEndings("\n").Split('\n'))
                .Any(line => line.StartsWith(start, StringComparison.Ordinal)),
            Describe());

    [Then("the run created {int} videos")]
    public void Created(int count) => Assert.Equal(count, _run!.Counts.Created);

    [Then("the run created {int} videos: {int} videos, {int} short(s) and {int} stream(s)")]
    public void CreatedByType(int count, int videos, int shorts, int streams)
    {
        Assert.Equal(count, _run!.Counts.Created);
        Assert.Equal(new TypeCounts(videos, shorts, streams), _run.Counts.CreatedByType);
    }

    [Then("the run updated {int} videos and left {int} unchanged")]
    public void UpdatedUnchanged(int updated, int unchanged) =>
        Assert.Equal((updated, unchanged), (_run!.Counts.Updated, _run.Counts.Unchanged));

    [Then("the run rejected {int} document(s)")]
    public void Rejected(int count) => Assert.Equal(count, _run!.Counts.Rejected);

    [Then("the run reports a rejected {string}")]
    public void RejectedSample(string subject) =>
        Assert.Contains(_run!.RejectedSamples, s => s.StartsWith(subject, StringComparison.Ordinal));

    [Then("the run corrected {int} media path(s)")]
    public void Corrected(int count) => Assert.Equal(count, _run!.Counts.Corrected);

    [Then("the run counted {int} subscription(s)")]
    public void CountedSubscriptions(int count) => Assert.True(_run!.Counts.Subscribed == count, Describe());

    [Then("the run counted {int} missing media file(s)")]
    public void MissingMedia(int count) => Assert.Equal(count, _run!.Counts.MediaMissing);

    [Then("TubeArchivist answered {int} video list request(s) during the run")]
    public void ListRequests(int count) =>
        Assert.Equal(count, Fake.CountRequests("/api/video/?") - _listRequestsBefore);

    // ---------------------------------------------------------------- TonTuyau's content

    [Then("TonTuyau has {int} videos and {int} channel(s)")]
    public async Task HasVideosAndChannels(int videos, int channels)
    {
        await RefreshAsync();
        Assert.Equal(
            (videos, channels),
            (await Videos.CountAsync(CancellationToken.None), await Channels.CountAsync(CancellationToken.None)));
    }

    [Then("TonTuyau has {int} videos")]
    public async Task HasVideos(int videos)
    {
        await RefreshAsync();
        Assert.Equal(videos, await Videos.CountAsync(CancellationToken.None));
    }

    [Then("TonTuyau has at least {int} videos of {string}")]
    public async Task AtLeast(int count, string channel)
    {
        await RefreshAsync();
        IReadOnlyList<VideoSummary> stored = await Videos.FindByChannelsAsync(
            [FakeTubeArchivist.ChannelId(channel)],
            CancellationToken.None);
        Assert.True(stored.Count >= count, $"{stored.Count} videos of {channel}");
    }

    [Then("TonTuyau has the video {string}")]
    public async Task HasVideo(string name) =>
        Assert.NotNull(await Videos.GetAsync(FakeTubeArchivist.VideoId(name), CancellationToken.None));

    [Then("TonTuyau doesn't have the video {string}")]
    public async Task HasNotVideo(string name)
    {
        await RefreshAsync();
        Assert.Null(await Videos.GetAsync(FakeTubeArchivist.VideoId(name), CancellationToken.None));
    }

    [Then("TonTuyau shows the video {string} with the title {string}")]
    public async Task ShowsTitle(string name, string title) =>
        Assert.Equal(title, (await Videos.GetAsync(FakeTubeArchivist.VideoId(name), CancellationToken.None))?.Title);

    [Then("the video {string} isn't a stream")]
    public async Task IsNotStream(string name) =>
        Assert.NotEqual(
            VideoType.Stream,
            (await Videos.GetAsync(FakeTubeArchivist.VideoId(name), CancellationToken.None))!.Type);

    [Then("the video {string} is a stream")]
    public async Task IsStream(string name) =>
        Assert.Equal(
            VideoType.Stream,
            (await Videos.GetAsync(FakeTubeArchivist.VideoId(name), CancellationToken.None))!.Type);

    [Then("the media path of {string} is in the folder of {string}")]
    public async Task MediaFolder(string name, string channel)
    {
        Video? video = await Videos.GetAsync(FakeTubeArchivist.VideoId(name), CancellationToken.None);
        Assert.StartsWith(FakeTubeArchivist.ChannelId(channel) + "/", video!.MediaPath, StringComparison.Ordinal);
        Assert.True(video.MediaPathCorrected);
    }

    [Then("the video {string} is flagged as missing its file")]
    public async Task FlaggedMissing(string name)
    {
        await RefreshAsync();
        StoredVideo? stored = await Videos.GetStoredAsync(FakeTubeArchivist.VideoId(name), CancellationToken.None);
        Assert.True(stored?.MediaMissing);
    }

    [Then("the video {string} isn't flagged as missing its file")]
    public async Task NotFlaggedMissing(string name)
    {
        await RefreshAsync();
        StoredVideo? stored = await Videos.GetStoredAsync(FakeTubeArchivist.VideoId(name), CancellationToken.None);
        Assert.False(stored?.MediaMissing);
    }

    // ---------------------------------------------------------------- art

    [Then("the art of every video and channel is copied")]
    public void AllArt()
    {
        foreach (FakeVideo video in Fake.Videos)
        {
            Assert.True(ArtExists(ThumbnailOf(video.Id)), video.Id);
        }

        foreach (FakeChannel channel in Fake.Channels)
        {
            AssertChannelArt(channel.Id);
        }
    }

    [Then("the art of channel {string} is copied")]
    public void ChannelArt(string name) => AssertChannelArt(FakeTubeArchivist.ChannelId(name));

    [Then("the art of {string} is copied")]
    public void VideoArt(string name) => Assert.True(ArtExists(ThumbnailOf(FakeTubeArchivist.VideoId(name))));

    [Then("the art of {string} is pending")]
    public async Task ArtPending(string name) =>
        Assert.Contains(ThumbnailOf(FakeTubeArchivist.VideoId(name)), await PendingArt());

    [Then("no art is pending")]
    public async Task NoArtPending() => Assert.Empty(await PendingArt());


    [Then("every thumbnail has its {string} px versions")]
    public void EveryThumbnailHasVersions(string widths)
    {
        foreach (FakeVideo video in Fake.Videos)
        {
            Assert.Equal(Widths(widths), VersionsOf(ThumbnailOf(video.Id)));
        }
    }

    [Then("every channel picture has its {string} px versions and every banner its {string} px version")]
    public void EveryChannelHasVersions(string thumbWidths, string bannerWidths)
    {
        foreach (FakeChannel channel in Fake.Channels)
        {
            Assert.Equal(Widths(thumbWidths), VersionsOf($"/cache/channels/{channel.Id}_thumb.jpg"));
            Assert.Equal(Widths(bannerWidths), VersionsOf($"/cache/channels/{channel.Id}_banner.jpg"));
            Assert.Empty(VersionsOf($"/cache/channels/{channel.Id}_tvart.jpg"));
        }
    }

    [Then("the thumbnail of {string} has the versions {string}")]
    public void ThumbnailVersions(string name, string widths) =>
        Assert.Equal(Widths(widths), VersionsOf(ThumbnailOf(FakeTubeArchivist.VideoId(name))));

    [Then("the run made {int} resized version(s)")]
    public void RunResized(int count) => Assert.Equal(count, _run!.Counts.ArtResized);

    [Then("the run failed to make {int} resized version(s)")]
    public void RunResizeFailed(int count) => Assert.Equal(count, _run!.Counts.ArtResizeFailed);

    [Then("the store records the thumbnail of {string} as {int} px wide with the versions {string}")]
    public async Task RecordedThumbnail(string name, int width, string widths)
    {
        await RefreshAsync();
        StoredVideo? stored = await Videos.GetStoredAsync(FakeTubeArchivist.VideoId(name), CancellationToken.None);
        Assert.Equal(new ArtVersions(width, Widths(widths)), stored!.ThumbnailArt);
    }

    [Then("the store records no versions for the thumbnail of {string}")]
    public async Task NothingRecorded(string name)
    {
        await RefreshAsync();
        StoredVideo? stored = await Videos.GetStoredAsync(FakeTubeArchivist.VideoId(name), CancellationToken.None);
        Assert.Null(stored!.ThumbnailArt);
    }

    [Then("the store records the picture and banner versions of every channel")]
    public async Task RecordedChannels()
    {
        await RefreshAsync();
        foreach (FakeChannel channel in Fake.Channels)
        {
            StoredChannel? stored = await Channels.GetStoredAsync(channel.Id, CancellationToken.None);
            Assert.Equal(new ArtVersions(400, [48, 96, 176, 320]), stored!.ThumbArt);
            Assert.Equal(new ArtVersions(1600, [1280]), stored.BannerArt);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _caller.Dispose();
        if (_otherProcessLease is not null)
        {
            await _otherProcessLease.DisposeAsync();
        }

        await _lettingGo;

        if (_fake is not null)
        {
            await _fake.DisposeAsync();
        }

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    // ---------------------------------------------------------------- helpers

    private static string ThumbnailOf(string videoId)
        => $"/cache/videos/{char.ToLowerInvariant(videoId[0])}/{videoId}.jpg";

    private void Set(string key, string value) => state.Settings.Set(ConfigKeys.EnvironmentName(key), value);

    private void Restart(string key, int value)
    {
        Set(key, value.ToString(CultureInfo.InvariantCulture));
        state.StopBackend();
    }

    private long NextDownload() =>
        Fake.Videos.Count == 0
            ? FakeTubeArchivist.Epoch.ToUnixTimeSeconds()
            : Math.Max(Fake.Videos.Max(v => v.Downloaded), FakeTubeArchivist.Epoch.ToUnixTimeSeconds()) + 86_400;

    private string NewName(string channel)
    {
        int count = Fake.Videos.Count(v => v.ChannelId == FakeTubeArchivist.ChannelId(channel)) + _removed.Count + 1;
        return $"{channel[..Math.Min(channel.Length, 5)]}-n{count:000}";
    }

    private void AddVideo(string name, string channel, long downloaded) =>
        Fake.Videos
            .Add(new FakeVideo(FakeTubeArchivist.VideoId(name), FakeTubeArchivist.ChannelId(channel), downloaded));

    private void Remove(FakeVideo video)
    {
        Fake.Videos.Remove(video);
        _removed[video.Id] = video;
    }

    private void WriteMediaFile(FakeVideo video)
    {
        string folder = Path.Combine(MediaRoot, video.ChannelId);
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, video.Id + ".mp4"), [0]);
    }

    private bool ArtExists(string cachePath) => File.Exists(Path.Combine(ArtRoot, cachePath["/cache/".Length..]));

    /// <summary>The resized versions on disk beside one image (<c>&lt;name&gt;.&lt;width&gt;.webp</c>), by width.</summary>
    private int[] VersionsOf(string cachePath)
    {
        string original = Path.Combine(ArtRoot, cachePath["/cache/".Length..]);
        string folder = Path.GetDirectoryName(original)!;
        string stem = Path.GetFileNameWithoutExtension(original);
        return Versions(folder)
            .Select(Path.GetFileName)
            .Where(f => f!.StartsWith(stem + ".", StringComparison.Ordinal))
            .Select(f => int.Parse(f![(stem.Length + 1)..^".webp".Length], CultureInfo.InvariantCulture))
            .Order()
            .ToArray();
    }

    private static IEnumerable<string> Versions(string folder) =>
        Directory.Exists(folder)
            ? Directory
                .EnumerateFiles(folder, "*.webp", SearchOption.AllDirectories)
                .Where(f => Path.GetFileNameWithoutExtension(f).Split('.')[^1].All(char.IsAsciiDigit))
            : [];

    private static int[] Widths(string text) =>
        text
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(w => int.Parse(w, CultureInfo.InvariantCulture))
            .ToArray();

    private void AssertChannelArt(string channelId)
    {
        foreach (string kind in (string[])["thumb", "banner", "tvart"])
        {
            Assert.True(ArtExists($"/cache/channels/{channelId}_{kind}.jpg"), $"{channelId} {kind}");
        }
    }

    private Task<IReadOnlyList<SyncRun>> RecentRuns() =>
        Services.GetRequiredService<ISyncStateStore>().GetRecentRunsAsync(50, CancellationToken.None);

    /// <summary>The run of that kind the backend started by itself at startup, once finished (in the background).</summary>
    private async Task<SyncRun> StartupRunAsync(SyncMode mode)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(90);
        while (true)
        {
            SyncRun? run = (await RecentRuns()).FirstOrDefault(r => r.Trigger == SyncTrigger.Startup && r.Mode == mode);
            // Over: not running, nor waiting in the queue still (2.0: a run starts queued; alpha.4, it was read as over).
            if (run is not null && run.Outcome is not (SyncOutcome.Queued or SyncOutcome.Running))
            {
                _run = run;
                return run;
            }

            if (DateTime.UtcNow > deadline)
            {
                string seen = string.Join(", ", (await RecentRuns()).Select(r => $"{r.Trigger} {r.Mode} {r.Outcome}"));
                Assert.Fail($"The backend didn't finish a startup {mode} within 90 s. Runs: {seen}.");
            }

            await Task.Delay(250);
        }
    }

    private Task<IReadOnlySet<string>> PendingArt() =>
        Services.GetRequiredService<ISyncStateStore>().GetPendingArtAsync(CancellationToken.None);

    private Task RefreshAsync() => Services.GetRequiredService<IIndexManager>().RefreshAsync(CancellationToken.None);

    private async Task RunAsync(Func<Task<SyncRun>> run)
    {
        _listRequestsBefore = Fake.CountRequests("/api/video/?");
        try
        {
            _run = await run();
        }
        catch (SyncBusyException ex)
        {
            state.Error = ex;
        }
        catch (SyncRefusedException ex)
        {
            state.Error = ex;
        }
    }

    private string Describe() =>
        _run is null
            ? "no run"
            : $"{_run.Outcome} {_run.Mode}: {_run.Error} | notes: {string.Join(" / ", _run.Notes)} "
                + $"| counts: {_run.Counts}";
}
