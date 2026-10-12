using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.Configuration;
using TonTuyau.Application.Library;
using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;
using TonTuyau.IntegrationTests.Support;

namespace TonTuyau.IntegrationTests.Steps;

[Binding]
public sealed class SyncSteps(ScenarioState state) : IAsyncDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "tontuyau-sync-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly List<SyncRun> _fullRuns = [];
    private readonly Dictionary<string, FakeVideo> _removed = new(StringComparer.Ordinal);
    private FakeTubeArchivist? _fake;
    private SyncRun? _run;
    private SyncRun? _applied;
    private readonly CancellationTokenSource _caller = new();
    private long? _watermarkBefore;
    private int _listRequestsBefore;
    private string? _lastDeletedVideo;
    private int _runsBeforeRestart;
    private bool _countedBeforeRestart;
    private ISyncLease? _otherProcessLease;

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
        Set(ConfigKeys.SyncLogRoot, Path.Combine(_root, "logs"));
        Set(ConfigKeys.SyncSchedulerEnabled, "false");
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
    [Given("the caller cancels the sync right before TubeArchivist answers page {int}")]
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
        _lastDeletedVideo = FakeTubeArchivist.VideoId(name);
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

    [Given("the format guard stops above {int} percent rejected, after {int} documents")]
    public void FormatGuard(int percent, int minimum)
    {
        Set(ConfigKeys.SyncRejectGuardPercent, percent.ToString(CultureInfo.InvariantCulture));
        Restart(ConfigKeys.SyncRejectGuardMinDocuments, minimum);
    }

    // ---------------------------------------------------------------- automatic index rebuild (QA10 M5-10)

    /// <summary>The scheduler on, with crons that never fire during a scenario (only the startup check matters).</summary>
    [Given("the scheduler is on, with no sync due soon")]
    public async Task SchedulerOn()
    {
        // Counted now, with the current backend: reading them later would start a backend with the scheduler on (and
        // its startup rebuild) before the restart the scenario is about.
        _runsBeforeRestart = (await RecentRuns()).Count;
        _countedBeforeRestart = true;
        Set(ConfigKeys.SyncSchedulerEnabled, "true");
        Set(ConfigKeys.SyncIncrementalCron, "0 0 1 1 *");
        Set(ConfigKeys.SyncFullCron, "0 0 1 1 *");
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

    [Then("an index rebuild started by the backend succeeds")]
    public async Task StartupRebuildSucceeds()
    {
        SyncRun run = await StartupRunAsync();
        Assert.Equal(SyncOutcome.Succeeded, run.Outcome);
        Assert.True(run.IndexRebuild);
    }

    [Then("an index rebuild started by the backend fails")]
    public async Task StartupRebuildFails()
    {
        SyncRun run = await StartupRunAsync();
        Assert.Equal(SyncOutcome.Failed, run.Outcome);
        Assert.True(run.IndexRebuild);
    }

    [Then("no sync starts by itself")]
    public async Task NoRunStarts()
    {
        // The startup check runs right after the host starts: give it time to do something if it were going to.
        await Task.Delay(TimeSpan.FromSeconds(3));
        Assert.Equal(_runsBeforeRestart, (await RecentRuns()).Count);
        Assert.Null(await Services.GetRequiredService<ISyncStateStore>().GetLockAsync(CancellationToken.None));
    }

    [Then("the run rebuilt the indices instead")]
    public void RebuiltInstead()
    {
        Assert.True(_run!.IndexRebuild);
        Assert.Equal(SyncMode.Full, _run.Mode);
        Assert.Contains(
            _run.Notes,
            n => n.StartsWith("Index rebuild instead of the incremental sync", StringComparison.Ordinal));
    }

    [Then("the run didn't rebuild the indices")]
    public void NotRebuilt() => Assert.False(_run!.IndexRebuild);

    [Given("the deletion limit is {int}")]
    public void DeletionLimit(int limit) => Restart(ConfigKeys.SyncMaxDeletions, limit);

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

    [Given("another process holds the sync lock")]
    public async Task OtherProcessHoldsLock() =>
        _otherProcessLease = await Services
            .GetRequiredService<ISyncStateStore>()
            .TryAcquireLockAsync(SyncMode.Full, SyncTrigger.Cli, CancellationToken.None);

    // ---------------------------------------------------------------- runs

    [Given("a first sync has run")]
    public async Task FirstSync()
    {
        await RunAsync(() => Sync.RunAsync(SyncMode.Incremental, SyncTrigger.Scheduler, CancellationToken.None));
        Assert.True(_run!.Outcome == SyncOutcome.Succeeded, $"The first sync failed: {_run.Error}");
    }

    [When("an incremental sync runs")]
    public Task Incremental()
        => RunAsync(() => Sync.RunAsync(SyncMode.Incremental, SyncTrigger.Scheduler, _caller.Token));

    [Given("a full sync has run")]
    [Given("another full sync has run")]
    [When("a full sync runs")]
    public Task Full() => RunAsync(() => Sync.RunAsync(SyncMode.Full, SyncTrigger.Scheduler, CancellationToken.None));

    [When("an index rebuild runs")]
    public Task Rebuild() => RunAsync(() => Sync.RebuildAsync(SyncTrigger.Cli, CancellationToken.None));

    [When("the deletions of that run are applied")]
    public Task ApplyLatest() => ApplyAsync(_fullRuns[^1].Id);

    [When("the deletions of the first of those runs are applied")]
    public Task ApplyFirst() => ApplyAsync(_fullRuns[^2].Id);

    // ---------------------------------------------------------------- outcomes

    [Then("the run succeeded")]
    public void Succeeded() => Assert.True(_run?.Outcome == SyncOutcome.Succeeded, Describe());

    [Then("the run succeeded as an incremental run")]
    public void SucceededIncremental()
    {
        Succeeded();
        Assert.Equal(SyncMode.Incremental, _run!.Mode);
    }

    [Then("the run failed")]
    public void Failed() => Assert.True(_run?.Outcome == SyncOutcome.Failed, Describe());

    [Then("the run was canceled, not failed")]
    public void Canceled()
    {
        Assert.True(_run?.Outcome == SyncOutcome.Canceled, Describe());
        Assert.Null(_run.Error);
        Assert.NotNull(_run.FinishedAt);
    }

    [Then("the stored run is canceled too")]
    public async Task StoredCanceled()
    {
        SyncRun? stored = await Services
            .GetRequiredService<ISyncStateStore>()
            .GetRunAsync(_run!.Id, CancellationToken.None);
        Assert.Equal(SyncOutcome.Canceled, stored?.Outcome);
    }

    [Then("the sync lock is free")]
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

    [Then("the run was escalated to a full run because {string}")]
    public void Escalated(string reason)
    {
        Assert.Equal(SyncMode.Full, _run!.Mode);
        Assert.Contains(
            _run.Notes,
            n => n.StartsWith("Escalated", StringComparison.Ordinal) && n.Contains(reason, StringComparison.Ordinal));
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

    [Then("the run deleted {int} item(s)")]
    public void Deleted(int count) => Assert.True(_run!.Counts.Deleted == count, Describe());

    [Then("the run kept {int} item(s) still in TubeArchivist")]
    public void Kept(int count) => Assert.Equal(count, _run!.Counts.KeptStillInTubeArchivist);

    [Then("the run's deletions are blocked by the limit")]
    public void Blocked() => Assert.True(_run!.DeletionsBlockedByLimit);

    [Then("the deletion list of the run has {int} entry/entries")]
    public async Task DeletionList(int count)
    {
        IReadOnlyList<DeletionEntry>? entries = await Services.GetRequiredService<IDeletionLog>()
            .ReadAsync(_run!.Id, CancellationToken.None);
        Assert.Equal(count, entries!.Count);
        Assert.Equal(count, _run.DeletionsPlanned);
    }

    [Then("{int} item(s) was/were deleted by the apply")]
    public void DeletedByApply(int count) => Assert.Equal(count, _applied!.Counts.Deleted);

    [Then("{int} item(s) was/were kept by the apply, still in TubeArchivist")]
    public void KeptByApply(int count) => Assert.Equal(count, _applied!.Counts.KeptStillInTubeArchivist);

    [Then("the apply is a run of its own, for the list of {int} entries of that full run")]
    public async Task ApplyIsItsOwnRun(int planned)
    {
        Assert.Equal(SyncMode.ApplyDeletions, _applied!.Mode);
        Assert.Equal(SyncTrigger.Cli, _applied.Trigger);
        Assert.Equal(SyncOutcome.Succeeded, _applied.Outcome);
        Assert.NotNull(_applied.FinishedAt);
        Assert.Equal(_fullRuns[^1].Id, _applied.AppliesTo);
        Assert.Equal(planned, _applied.DeletionsPlanned);
        Assert.NotEqual(_fullRuns[^1].Id, _applied.Id);
        SyncRun? stored = await Services
            .GetRequiredService<ISyncStateStore>()
            .GetRunAsync(_applied.Id, CancellationToken.None);
        Assert.Equal(_applied.Counts.Deleted, stored?.Counts.Deleted);
    }

    [Then("the full run's record is as that run left it")]
    public async Task FullRunUntouched()
    {
        SyncRun full = _fullRuns[^1];
        SyncRun? stored = await Services
            .GetRequiredService<ISyncStateStore>()
            .GetRunAsync(full.Id, CancellationToken.None);
        Assert.Equal(full.Counts, stored?.Counts);
        Assert.Equal(full.Notes, stored?.Notes);
        Assert.Equal(full.FinishedAt, stored?.FinishedAt);
    }

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

    [Then("the watermark is the download date of the newest video")]
    public async Task WatermarkNewest() =>
        Assert.Equal(Fake.Videos.Max(v => v.Downloaded), (await Checkpoint()).Watermark);

    [Then("the watermark didn't move")]
    public async Task WatermarkUnchanged() => Assert.Equal(_watermarkBefore, (await Checkpoint()).Watermark);

    [Then("the video {string} is flagged as missing its file")]
    public async Task FlaggedMissing(string name)
    {
        await RefreshAsync();
        Assert.Contains(FakeTubeArchivist.VideoId(name), await Videos.GetMediaMissingIdsAsync(CancellationToken.None));
    }

    [Then("the video {string} isn't flagged as missing its file")]
    public async Task NotFlaggedMissing(string name)
    {
        await RefreshAsync();
        Assert.DoesNotContain(
            FakeTubeArchivist.VideoId(name),
            await Videos.GetMediaMissingIdsAsync(CancellationToken.None));
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

    [Then("its thumbnail is removed from the art volume")]
    public void ThumbnailRemoved() => Assert.False(ArtExists(ThumbnailOf(_lastDeletedVideo!)));

    // ---------------------------------------------------------------- resized art (QA09)

    [Given("the resized versions are deleted from the art volume")]
    public void DeleteVersions()
    {
        foreach (string file in Versions(ArtRoot))
        {
            File.Delete(file);
        }
    }

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

    private Task<SyncCheckpoint> Checkpoint() =>
        Services.GetRequiredService<ISyncStateStore>().GetCheckpointAsync(CancellationToken.None);

    private Task<IReadOnlyList<SyncRun>> RecentRuns() =>
        Services.GetRequiredService<ISyncStateStore>().GetRecentRunsAsync(50, CancellationToken.None);

    /// <summary>The run the backend started by itself at startup, once finished (it runs in the background).</summary>
    private async Task<SyncRun> StartupRunAsync()
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(90);
        while (true)
        {
            SyncRun? run = (await RecentRuns()).FirstOrDefault(r => r.Trigger == SyncTrigger.Startup);
            if (run is { Outcome: not SyncOutcome.Running })
            {
                _run = run;
                return run;
            }

            if (DateTime.UtcNow > deadline)
            {
                string seen = string.Join(", ", (await RecentRuns()).Select(r => $"{r.Trigger} {r.Mode} {r.Outcome}"));
                Assert.Fail($"The backend didn't finish a startup rebuild within 90 s. Runs: {seen}.");
            }

            await Task.Delay(250);
        }
    }

    private Task<IReadOnlySet<string>> PendingArt() =>
        Services.GetRequiredService<ISyncStateStore>().GetPendingArtAsync(CancellationToken.None);

    private Task RefreshAsync() => Services.GetRequiredService<IIndexManager>().RefreshAsync(CancellationToken.None);

    private async Task RunAsync(Func<Task<SyncRun>> run)
    {
        _watermarkBefore = (await Checkpoint()).Watermark;
        _listRequestsBefore = Fake.CountRequests("/api/video/?");
        try
        {
            _run = await run();
            if (_run.Mode == SyncMode.Full)
            {
                _fullRuns.Add(_run);
            }
        }
        catch (SyncBusyException ex)
        {
            state.Error = ex;
        }
    }

    private async Task ApplyAsync(string runId)
    {
        try
        {
            _applied = await Sync.ApplyDeletionsAsync(runId, SyncTrigger.Cli, CancellationToken.None);
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
