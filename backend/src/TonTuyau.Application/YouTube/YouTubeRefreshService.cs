using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using TonTuyau.Application.Library;
using TonTuyau.Application.RunLogs;
using TonTuyau.Application.Store;
using TonTuyau.Application.Streaming;
using TonTuyau.Application.Sync;
using TonTuyau.Application.Validation;

namespace TonTuyau.Application.YouTube;

/// <summary>
/// Reads videos and channels of the library on YouTube (1.5), as runs like the import (the same lock, history, log):
/// a video, a channel, or a channel and each of its videos. What YouTube answers becomes TonTuyau's facts
/// (PostgreSQL), then the index is updated with them:
/// <list type="bullet">
/// <item>found: on YouTube, and its metadata from now on wins over TubeArchivist's;</item>
/// <item>gone (removed, private, terminated): gone, what TonTuyau read before stays;</item>
/// <item>no answer to trust: retry later, unless TonTuyau already knows better (on YouTube or gone): a failed check
/// changes nothing it confirmed (the index is still written with it).</item>
/// </list>
/// </summary>
/// <remarks>
/// A video's chapters are read with it; its comments too when the YouTube settings say so (the top ones, answers
/// included, YouTube's order, or all of them), replacing the ones read before. Its thumbnail, a channel's avatar and
/// banner, go to TonTuyau's art volume, replacing TubeArchivist's copies; a picture that fails never fails the
/// refresh (the one there stays).
/// <para>
/// A download (2.0, <see cref="SyncMode.DownloadVideo"/>) is a run of the same kind: yt-dlp downloads the video into a
/// folder of its own on the media folder, the file goes to its place, the video is added to the library with what
/// YouTube said of it (as a refresh records it), with its channel first when the library doesn't have it (then read on
/// YouTube like a refresh, for its pictures). YouTube refusing puts it on hold, as a refresh.
/// </para>
/// </remarks>
public sealed partial class YouTubeRefreshService(
    IYouTubeClient youTube,
    IYouTubeFactsSource facts,
    IYouTubeFactsStore store,
    IYouTubeSettingsRepository settings,
    IYouTubeImages images,
    IArtStore art,
    IArtResizer resizer,
    IMediaLibrary media,
    IVideoRepository videos,
    IChannelRepository channels,
    ISubscriptionStore subscriptions,
    IDownloadFailureStore downloadFailures,
    ISyncStateStore state,
    StreamingService streaming,
    DownloadSpace space,
    TimeProvider clock,
    ILogger<YouTubeRefreshService> logger)
{
    /// <summary>"Refresh everything" skips what was read in the last this many days, unless told otherwise (B8).</summary>
    public const int DefaultSkipReadDays = 7;

    /// <param name="mode">A refresh: <see cref="SyncMode.RefreshVideo"/>, <see cref="SyncMode.RefreshChannel"/> or
    /// <see cref="SyncMode.RefreshChannelVideos"/>; or a download, <see cref="SyncMode.DownloadVideo"/>.</param>
    /// <param name="target">
    /// The video's or channel's YouTube id: one of the library for a refresh, one it doesn't have for a download.
    /// </param>
    /// <param name="startedBy">The account that started it from the admin pages; null otherwise.</param>
    /// <exception cref="SyncRefusedException">
    /// Not an id, not one of the library (a refresh), or one of the library already (a download).
    /// </exception>
    /// <exception cref="SyncBusyException">Another run holds the lock.</exception>
    public async Task<SyncRun> RunAsync(
        SyncMode mode,
        string target,
        SyncTrigger trigger,
        string? startedBy,
        ISyncProgressReporter progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        await CheckAsync(mode, target, cancellationToken).ConfigureAwait(false);
        return await StartAsync(mode, target, trigger, startedBy, null, progress, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Runs a queued refresh or download (2.0, the run queue): the same run goes on, from <c>queued</c>. One canceled
    /// meanwhile is left as it is; one whose target no longer fits (a video added since, for a download) fails, saying
    /// why.
    /// </summary>
    /// <exception cref="SyncBusyException">Another run holds the lock: it stays queued.</exception>
    public async Task<SyncRun> RunQueuedAsync(
        SyncRun queued,
        ISyncProgressReporter progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queued);
        ArgumentNullException.ThrowIfNull(progress);
        try
        {
            await CheckAsync(queued.Mode, queued.Target ?? "", cancellationToken).ConfigureAwait(false);
        }
        catch (SyncRefusedException ex)
        {
            SyncRun refused = queued with
            {
                Outcome = SyncOutcome.Failed,
                Error = ex.Message,
                FinishedAt = clock.GetUtcNow(),
            };
            await state.SaveRunAsync(refused, cancellationToken).ConfigureAwait(false);
            return refused;
        }

        Task<SyncRun> run = StartAsync(
            queued.Mode,
            queued.Target,
            queued.Trigger,
            queued.StartedBy,
            queued,
            progress,
            cancellationToken);
        return await run.ConfigureAwait(false);
    }

    /// <param name="target">Its video or channel; null for a YouTube sync (B5).</param>
    /// <param name="queued">The queued run to go on with; null: a new run.</param>
    private async Task<SyncRun> StartAsync(
        SyncMode mode,
        string? target,
        SyncTrigger trigger,
        string? startedBy,
        SyncRun? queued,
        ISyncProgressReporter progress,
        CancellationToken cancellationToken)
    {
        // What it reads, in order: the channel first, then its videos, latest downloaded first; not the gone ones.
        // A YouTube sync (B5): every subscribed channel, the earliest subscribed first (a full sync of one channel: that
        // one); each one's videos to download come right after it, once it's listed.
        // Refresh everything (B8): the library's channels and videos, the least recently read first.
        (IReadOnlyList<string> Work, int Skipped) everything = mode == SyncMode.RefreshAll
            ? RefreshAllPlan.Order(
                await facts.GetAllChecksAsync(cancellationToken).ConfigureAwait(false),
                clock.GetUtcNow(),
                queued?.SkipReadDays ?? DefaultSkipReadDays)
            : ([], 0);
        List<string> work = mode is SyncMode.YouTubeSync or SyncMode.YouTubeFullSync && target is null
            ? [.. (await subscriptions.ListAsync(cancellationToken).ConfigureAwait(false)).Select(s => s.ChannelId)]
            : mode == SyncMode.RefreshAll
            ? [.. everything.Work]
            : mode == SyncMode.RetryDownloads
            ? [.. (await downloadFailures.ListAsync(target!, cancellationToken).ConfigureAwait(false))
                .Select(f => f.VideoId)]
            : [target!];
        List<VideoSummary> skipped = [];
        if (mode == SyncMode.RefreshChannelVideos)
        {
            IReadOnlyList<VideoSummary> ofChannel = await videos
                .FindByChannelsAsync([target!], cancellationToken)
                .ConfigureAwait(false);
            IReadOnlyDictionary<string, VideoFacts> known = await facts
                .GetVideosAsync([.. ofChannel.Select(v => v.Id)], cancellationToken)
                .ConfigureAwait(false);
            foreach (VideoSummary video in ofChannel.OrderByDescending(v => v.Downloaded))
            {
                if (known.GetValueOrDefault(video.Id)?.Status.State == YouTubeState.Gone)
                {
                    skipped.Add(video);
                }
                else
                {
                    work.Add(video.Id);
                }
            }
        }

        ISyncLease lease = await state.TryAcquireLockAsync(mode, trigger, cancellationToken).ConfigureAwait(false)
            ?? throw new SyncBusyException(await state.GetLockAsync(cancellationToken).ConfigureAwait(false));
        await using (lease.ConfigureAwait(false))
        {
            SyncRun run;
            if (queued is null)
            {
                run = new SyncRun
                {
                    Id = SyncRun.NewId(clock.GetUtcNow()),
                    Mode = mode,
                    Trigger = trigger,
                    StartedAt = clock.GetUtcNow(),
                    StartedBy = startedBy,
                    Target = target,
                };
            }
            else
            {
                // Again under the lock: it may have been canceled while it waited.
                SyncRun? current = await state.GetRunAsync(queued.Id, cancellationToken).ConfigureAwait(false);
                if (current?.Outcome != SyncOutcome.Queued)
                {
                    return current ?? queued;
                }

                run = current with { Outcome = SyncOutcome.Running, StartedAt = clock.GetUtcNow() };
            }

            if (mode == SyncMode.RefreshAll)
            {
                int days = run.SkipReadDays ?? DefaultSkipReadDays;
                run = run with
                {
                    Notes =
                    [
                        .. run.Notes,
                        $"{work.Count} channels and videos to read, the least recently read first; {everything.Skipped} "
                            + $"skipped: gone, or read in the last {days} days.",
                    ],
                };
            }

            return await RunLockedAsync(run, work, skipped, resumedBy: null, lease, progress, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Resumes a run on hold (<see cref="SyncOutcome.OnHold"/>): the same run, from where YouTube refused it.
    /// </summary>
    /// <param name="by">Who asks (an account); null: by itself, after the cool-down.</param>
    /// <exception cref="SyncRefusedException">No such run, or it isn't on hold.</exception>
    /// <exception cref="SyncBusyException">Another run holds the lock.</exception>
    public async Task<SyncRun> ResumeAsync(
        string runId,
        SyncTrigger trigger,
        string? by,
        ISyncProgressReporter progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        SyncRun held = await HeldAsync(runId, cancellationToken).ConfigureAwait(false);
        ISyncLease lease = await state.TryAcquireLockAsync(held.Mode, trigger, cancellationToken).ConfigureAwait(false)
            ?? throw new SyncBusyException(await state.GetLockAsync(cancellationToken).ConfigureAwait(false));
        await using (lease.ConfigureAwait(false))
        {
            // Again under the lock: it may have been resumed or canceled meanwhile.
            held = await HeldAsync(runId, cancellationToken).ConfigureAwait(false);
            SyncRun run = held with
            {
                Outcome = SyncOutcome.Running,
                FinishedAt = null,
                ResumeAt = null,
                Remaining = [],
            };
            return await RunLockedAsync(run, [.. held.Remaining], [], by ?? Itself, lease, progress, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>Cancels a run on hold: it won't resume; what it did stays.</summary>
    /// <param name="by">Who asks, for its notes.</param>
    /// <exception cref="SyncRefusedException">No such run, or it isn't on hold.</exception>
    /// <exception cref="SyncBusyException">Another run holds the lock (it may be this one, resuming).</exception>
    public async Task<SyncRun> CancelHeldAsync(string runId, string by, CancellationToken cancellationToken)
    {
        SyncRun held = await HeldAsync(runId, cancellationToken).ConfigureAwait(false);
        ISyncLease lease = await state.TryAcquireLockAsync(held.Mode, SyncTrigger.Admin, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new SyncBusyException(await state.GetLockAsync(cancellationToken).ConfigureAwait(false));
        await using (lease.ConfigureAwait(false))
        {
            held = await HeldAsync(runId, cancellationToken).ConfigureAwait(false);
            SyncRun canceled = held with
            {
                Outcome = SyncOutcome.Canceled,
                ResumeAt = null,
                Notes = [.. held.Notes, $"Canceled by {by} while on hold: it won't resume."],
            };
            await state.SaveRunAsync(canceled, CancellationToken.None).ConfigureAwait(false);
            using IDisposable? scope = logger.BeginScope(new RunLogScope(runId));
            LogCanceledHeld(runId, by);
            return canceled;
        }
    }

    /// <summary>"by itself", in a resumed run's note.</summary>
    private const string Itself = "itself, after the cool-down";

    // The refresh going on here, for a hold by hand (one run at a time): its id, what stops it, who asked.
    private readonly Lock _holdGate = new();
    private string? _runningId;
    private CancellationTokenSource? _holding;
    private string? _holdBy;

    /// <summary>
    /// Puts the refresh going on here on hold now, by hand (1.5): it stops at once, the item it was reading kept to
    /// read again, and waits for Resume now (no cool-down). False when it isn't the refresh going on here.
    /// </summary>
    public bool RequestHold(string runId, string by)
    {
        lock (_holdGate)
        {
            if (_runningId != runId)
            {
                return false;
            }

            if (_holdBy is null)
            {
                _holdBy = by;
                // Under the gate: the run lets go of its source under it too, so it's never canceled once disposed.
                _holding!.Cancel();
            }

            return true;
        }
    }

    private void Watch(string runId, CancellationTokenSource holding)
    {
        lock (_holdGate)
        {
            _runningId = runId;
            _holding = holding;
            _holdBy = null;
        }
    }

    private void Unwatch()
    {
        lock (_holdGate)
        {
            _runningId = null;
            _holding = null;
            _holdBy = null;
        }
    }

    /// <summary>Who put the refresh going on on hold by hand; null when nobody did.</summary>
    private string? HoldRequestedBy()
    {
        lock (_holdGate)
        {
            return _holdBy;
        }
    }

    private async Task<SyncRun> HeldAsync(string runId, CancellationToken cancellationToken)
    {
        SyncRun? run = await state.GetRunAsync(runId, cancellationToken).ConfigureAwait(false)
            ?? throw new SyncRefusedException($"No run {runId}.");
        return run.Outcome == SyncOutcome.OnHold
            ? run
            : throw new SyncRefusedException($"The run {runId} isn't on hold.");
    }

    /// <summary>
    /// Runs (or resumes) a refresh under the lock: reads <paramref name="work"/> in order (channels and videos), and
    /// puts it on hold when YouTube refuses, or when an admin asks (the lock is then let go, what's left kept with the
    /// run).
    /// </summary>
    /// <param name="skipped">The gone videos left out of <paramref name="work"/>: said in its log and a note.</param>
    /// <param name="resumedBy">Who resumed it; null for a new run.</param>
    private async Task<SyncRun> RunLockedAsync(
        SyncRun run,
        List<string> work,
        List<VideoSummary> skipped,
        string? resumedBy,
        ISyncLease lease,
        ISyncProgressReporter progress,
        CancellationToken cancellationToken)
    {
        // Stopping (canceled, the lock lost) ends the run; holding (by hand) keeps what's left for later.
        using CancellationTokenSource stopping = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            lease.Lost);
        using CancellationTokenSource holding = new();
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            stopping.Token,
            holding.Token);
        using ProgressLines logged = new(LogProgress, clock);
        RunContext context = new(
            state,
            run,
            new HashSet<string>(),
            new BothProgress(progress, logged),
            clock.GetUtcNow(),
            linked.Token);
        if (resumedBy is not null)
        {
            context.Note($"Resumed by {resumedBy}.");
        }

        await context.SaveAsync().ConfigureAwait(false);
        using IDisposable? scope = logger.BeginScope(new RunLogScope(run.Id));
        if (resumedBy is null)
        {
            LogStarted(run.Id, run.Mode, run.Target ?? "", run.Trigger);
        }
        else
        {
            LogResumed(run.Id, resumedBy, work.Count);
        }

        foreach (VideoSummary video in skipped)
        {
            LogSkippedGone(video.Id, video.Title);
        }

        if (skipped.Count > 0)
        {
            context.Note(skipped.Count == 1
                ? "1 gone video skipped for the refresh: it isn't read any more."
                : $"{skipped.Count} gone videos skipped for the refresh: they aren't read any more.");
        }

        Watch(run.Id, holding);
        progress.Started(run.Id);
        try
        {
            // Read once: a change on the YouTube page applies from the next run (or resume).
            YouTubeSettings read = await settings.GetAsync(stopping.Token).ConfigureAwait(false);
            YouTubeCommentsRequest? comments = read.CommentsRequest;
            LogComments(comments is null ? "none" : comments.Max?.ToString(CultureInfo.InvariantCulture) ?? "all");
            if (await WorkAsync(run.Mode, work, read, comments, context, stopping.Token).ConfigureAwait(false))
            {
                context.Update(r => r with { Outcome = SyncOutcome.Succeeded });
            }
        }
        catch (Exception ex) when (IsRunFailure(ex))
        {
            bool lockLost = lease.Lost.IsCancellationRequested;
            if (cancellationToken.IsCancellationRequested && !lockLost)
            {
                context.Update(r => r with { Outcome = SyncOutcome.Canceled });
            }
            else
            {
                string reason = lockLost
                    ? "The run lock was lost (another process holds it): stopped."
                    : RunErrors.ReasonOf(ex);
                context.Update(r => r with { Outcome = SyncOutcome.Failed, Error = reason });
                LogFailed(run.Id, reason);
            }
        }
        catch (Exception ex)
        {
            // Not a failure a run expects (a bug): still recorded as over, not left running, then thrown on.
            LogUnexpected(ex, run.Id);
            context.Update(r => r with { Outcome = SyncOutcome.Failed, Error = RunErrors.ReasonOf(ex) });
            throw;
        }
        finally
        {
            Unwatch();
            // On hold too: it's when it stopped, until it resumes.
            context.Update(r => r with { FinishedAt = clock.GetUtcNow() });
            await context.SaveAsync().ConfigureAwait(false);
        }

        logged.Finish();
        LogFinished(context.Run.Id, context.Run.Outcome);
        return context.Run;
    }

    /// <summary>Reads each channel or video of <paramref name="work"/>, a pause between two videos.</summary>
    /// <returns>False when YouTube refused and the run is now on hold (what's left kept with it).</returns>
    private async Task<bool> WorkAsync(
        SyncMode mode,
        List<string> work,
        YouTubeSettings read,
        YouTubeCommentsRequest? comments,
        RunContext context,
        CancellationToken stopping)
    {
        CancellationToken ct = context.CancellationToken;
        int videosTotal = context.Run.Counts.VideosRead + work.Count(LibraryIds.IsVideoId);
        bool videosBegun = false;
        for (int index = 0; index < work.Count; index++)
        {
            // Its counts as before it: an item stopped by a hold is read again when the run resumes.
            SyncCounts before = context.Run.Counts;
            try
            {
                ct.ThrowIfCancellationRequested();
                string id = work[index];
                string? refused;
                if (mode == SyncMode.DownloadVideo)
                {
                    context.BeginPhase(SyncPhase.YouTubeDownload, 1);
                    refused = await DownloadOneAsync(id, read, context).ConfigureAwait(false);
                }
                else if (mode == SyncMode.AddChannel)
                {
                    context.BeginPhase(SyncPhase.YouTubeChannel);
                    refused = await AddChannelAsync(id, context).ConfigureAwait(false);
                }
                else if (mode is SyncMode.YouTubeSync or SyncMode.YouTubeFullSync or SyncMode.RetryDownloads)
                {
                    refused = await SyncItemAsync(mode == SyncMode.YouTubeFullSync, work, index, read, context)
                        .ConfigureAwait(false);
                }
                else if (mode == SyncMode.RefreshAll)
                {
                    // One line over the whole library; a pause before each read but the first.
                    if (index == 0)
                    {
                        context.BeginPhase(SyncPhase.YouTubeLibrary, work.Count);
                    }
                    else
                    {
                        await PauseAsync(read.PauseSeconds, ct).ConfigureAwait(false);
                    }

                    refused = LibraryIds.IsChannelId(id)
                        ? await RefreshChannelAsync(id, context, counted: true).ConfigureAwait(false)
                        : await RefreshVideoAsync(id, comments, context).ConfigureAwait(false);
                    context.Progressed(index + 1);
                }
                else if (LibraryIds.IsChannelId(id))
                {
                    context.BeginPhase(SyncPhase.YouTubeChannel);
                    // With its videos, the run counts what YouTube answered of them; the channel's answer is a note.
                    refused = await RefreshChannelAsync(id, context, counted: mode == SyncMode.RefreshChannel)
                        .ConfigureAwait(false);
                }
                else
                {
                    if (videosBegun)
                    {
                        await PauseAsync(read.PauseSeconds, ct).ConfigureAwait(false);
                    }
                    else
                    {
                        videosBegun = true;
                        context.BeginPhase(SyncPhase.YouTubeVideos, videosTotal);
                        context.Progressed(context.Run.Counts.VideosRead);
                    }

                    refused = await RefreshVideoAsync(id, comments, context).ConfigureAwait(false);
                    context.Progressed(context.Run.Counts.VideosRead);
                }

                if (refused is not null)
                {
                    Hold([.. work.Skip(index)], refused, read.HoldMinutes, context);
                    return false;
                }
            }
            catch (DiskSpaceLowException ex)
            {
                // 2.2.0: under the disk space threshold, the download not begun: read again when the run resumes.
                context.Count(_ => before);
                HoldForSpace([.. work.Skip(index)], ex.Message, read.HoldMinutes, context);
                return false;
            }
            catch (Exception) when (!stopping.IsCancellationRequested && HoldRequestedBy() is { } by)
            {
                context.Count(_ => before);
                HoldByHand([.. work.Skip(index)], by, context);
                return false;
            }

            // The run's page shows the run as saved: its counts so far, after each answer.
            await context.SaveAsync().ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>YouTube refuses: the run waits, on hold, from <paramref name="left"/> (the one refused first).</summary>
    private void Hold(string[] left, string reason, int minutes, RunContext context)
    {
        DateTimeOffset now = clock.GetUtcNow();
        DateTimeOffset resumeAt = now.AddMinutes(minutes);
        context.Update(r => r with { Outcome = SyncOutcome.OnHold, ResumeAt = resumeAt, Remaining = left });
        context.Note(string.Create(
            CultureInfo.InvariantCulture,
            $"On hold at {now:yyyy-MM-dd HH:mm} UTC, {left.Length} left to read: YouTube refused ({reason}). It resumes by itself at {resumeAt:HH:mm} UTC, or with Resume now."));
        LogHeld(context.Run.Id, reason, resumeAt, left.Length);
    }

    /// <summary>
    /// The disk space is under the threshold (2.2.0): the run waits, on hold, from <paramref name="left"/>; resumed by
    /// itself after the cool-down, it holds again while there isn't room.
    /// </summary>
    private void HoldForSpace(string[] left, string why, int minutes, RunContext context)
    {
        DateTimeOffset now = clock.GetUtcNow();
        DateTimeOffset resumeAt = now.AddMinutes(minutes);
        context.Update(r => r with { Outcome = SyncOutcome.OnHold, ResumeAt = resumeAt, Remaining = left });
        context.Note(string.Create(
            CultureInfo.InvariantCulture,
            $"On hold at {now:yyyy-MM-dd HH:mm} UTC, {left.Length} left to read: {why} It tries again by itself at {resumeAt:HH:mm} UTC, or with Resume now."));
        LogHeldForSpace(context.Run.Id, resumeAt, left.Length);
    }

    private void HoldByHand(string[] left, string by, RunContext context)
    {
        DateTimeOffset now = clock.GetUtcNow();
        context.Update(r => r with { Outcome = SyncOutcome.OnHold, ResumeAt = null, Remaining = left });
        context.Note(string.Create(
            CultureInfo.InvariantCulture,
            $"On hold at {now:yyyy-MM-dd HH:mm} UTC, {left.Length} left to read: put on hold by {by}. It resumes only with Resume now."));
        LogHeldByHand(context.Run.Id, by, left.Length);
    }

    /// <summary>A random wait of half to 1.5 times <paramref name="seconds"/> (as TubeArchivist does); 0: none.</summary>
    private async Task PauseAsync(int seconds, CancellationToken cancellationToken)
    {
        if (seconds <= 0)
        {
            return;
        }

        int wait = RandomNumberGenerator.GetInt32(seconds / 2, (seconds * 3 / 2) + 1);
        await Task.Delay(TimeSpan.FromSeconds(wait), clock, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Refuses a refresh of what the library doesn't have, a download of what it has (and a non-id).</summary>
    /// <exception cref="SyncRefusedException">Why it can't run.</exception>
    public async Task CheckAsync(SyncMode mode, string target, CancellationToken cancellationToken)
    {
        // The downloads (2.2.0): under the disk space threshold, set to wait, none starts.
        if (mode is SyncMode.DownloadVideo
                or SyncMode.YouTubeSync
                or SyncMode.YouTubeFullSync
                or SyncMode.RetryDownloads
            && await space.WhyWaitAsync(cancellationToken).ConfigureAwait(false) is { } wait)
        {
            throw new SyncRefusedException(wait);
        }

        // A sync of every subscription, a refresh of everything: nothing to check (none is nothing to do).
        if (mode is SyncMode.YouTubeSync or SyncMode.RefreshAll
            || (mode == SyncMode.YouTubeFullSync && target.Length == 0))
        {
            return;
        }

        if (mode == SyncMode.DownloadVideo)
        {
            if (!LibraryIds.IsVideoId(target))
            {
                throw new SyncRefusedException($"{target} isn't a YouTube video id.");
            }

            // TubeArchivist's or TonTuyau's: a video of the library isn't downloaded again (B3), unless its file is
            // missing: then its file is.
            if (await videos.GetStoredAsync(target, cancellationToken).ConfigureAwait(false) is { MediaMissing: false }
                stored)
            {
                throw new SyncRefusedException($"The video \"{stored.Video.Title}\" is in the library already.");
            }
        }
        else if (mode == SyncMode.RefreshVideo)
        {
            Video? video = LibraryIds.IsVideoId(target)
                ? await videos.GetAsync(target, cancellationToken).ConfigureAwait(false)
                : null;
            if (video is null)
            {
                throw new SyncRefusedException($"No video {target} in the library.");
            }

            IReadOnlyDictionary<string, VideoFacts> known = await facts
                .GetVideosAsync([target], cancellationToken)
                .ConfigureAwait(false);
            if (known.GetValueOrDefault(target)?.Status.State == YouTubeState.Gone)
            {
                throw new SyncRefusedException(
                    $"The video \"{video.Title}\" is gone from YouTube: it isn't refreshed any more.");
            }
        }
        else if (mode == SyncMode.RetryDownloads)
        {
            if (!LibraryIds.IsChannelId(target)
                || (await downloadFailures.ListAsync(target, cancellationToken).ConfigureAwait(false)).Count == 0)
            {
                throw new SyncRefusedException($"No failed download of the channel {target} to try again.");
            }
        }
        else if (mode == SyncMode.YouTubeFullSync)
        {
            // "Download everything" of one channel: its subscription says which tabs.
            if (!LibraryIds.IsChannelId(target)
                || await subscriptions.GetAsync(target, cancellationToken).ConfigureAwait(false) is null)
            {
                throw new SyncRefusedException(
                    $"TonTuyau isn't subscribed to the channel {target}: subscribe to it first, with its tabs.");
            }
        }
        else if (mode == SyncMode.AddChannel)
        {
            if (!LibraryIds.IsChannelId(target) && !LibraryIds.IsHandle(target))
            {
                throw new SyncRefusedException($"{target} isn't a YouTube channel's id or @handle.");
            }

            // A handle's channel is known once YouTube gave its id: the run checks it then.
            if (LibraryIds.IsChannelId(target)
                && await subscriptions.GetAsync(target, cancellationToken).ConfigureAwait(false) is not null)
            {
                throw new SyncRefusedException(await SubscribedAlreadyAsync(target, cancellationToken)
                    .ConfigureAwait(false));
            }
        }
        else if (mode is SyncMode.RefreshChannel or SyncMode.RefreshChannelVideos)
        {
            if (!LibraryIds.IsChannelId(target)
                || await channels.GetAsync(target, cancellationToken).ConfigureAwait(false) is null)
            {
                throw new SyncRefusedException($"No channel {target} in the library.");
            }
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Not a refresh.");
        }
    }

    /// <param name="comments">Its comments too (the YouTube settings); null: not read, those read before stay.</param>
    /// <returns>What YouTube said when it refused (nothing is recorded then); null otherwise.</returns>
    private async Task<string?> RefreshVideoAsync(string videoId, YouTubeCommentsRequest? comments, RunContext context)
    {
        CancellationToken ct = context.CancellationToken;
        // Read again: a video deleted since the run started is skipped.
        Video? video = await videos.GetAsync(videoId, ct).ConfigureAwait(false);
        if (video is null)
        {
            return null;
        }

        YouTubeLookup<YouTubeVideo> lookup = await youTube
            .GetVideoAsync(videoId, comments, ct)
            .ConfigureAwait(false);
        if (lookup.Answer == YouTubeAnswer.Refused)
        {
            return lookup.Reason ?? "YouTube refused";
        }

        context.Count(c => c with { VideosRead = c.VideosRead + 1 });
        VideoFacts? known = (await facts.GetVideosAsync([videoId], ct).ConfigureAwait(false))
            .GetValueOrDefault(videoId);
        DateTimeOffset now = clock.GetUtcNow();
        SavedPicture? thumbnail = null;
        if (lookup.Item is not null)
        {
            Picture wanted = new(
                YouTubePictures.Thumbnail(lookup.Item.Thumbnails),
                video.ThumbnailPath,
                YouTubePictures.VideoStem(videoId),
                ArtSubject.VideoThumbnail);
            thumbnail = await SavePictureAsync(wanted, context).ConfigureAwait(false);
        }
        VideoFacts learned = lookup.Answer switch
        {
            YouTubeAnswer.Found => new VideoFacts(
                videoId,
                video.ChannelId,
                new YouTubeStatus(StateOf(lookup.Item!), now, null, now),
                RefreshedOf(lookup.Item!, video, known?.Refreshed, thumbnail?.Path)),
            YouTubeAnswer.Gone or YouTubeAnswer.Private => new VideoFacts(
                videoId,
                video.ChannelId,
                new YouTubeStatus(
                    lookup.Answer == YouTubeAnswer.Gone ? YouTubeState.Gone : YouTubeState.Private,
                    now,
                    lookup.Reason,
                    known?.Status.RefreshedAt),
                known?.Refreshed),
            _ when IsConfirmed(known?.Status) => known!,
            _ => new VideoFacts(
                videoId,
                video.ChannelId,
                new YouTubeStatus(YouTubeState.RetryLater, now, lookup.Reason, known?.Status.RefreshedAt),
                known?.Refreshed),
        };
        Counted(videoId, lookup, context);
        if (ReferenceEquals(learned, known))
        {
            LogKept(videoId);
        }
        else
        {
            await store.SaveVideoAsync(learned, ct).ConfigureAwait(false);
        }

        // Null: yt-dlp couldn't read them; those read before stay.
        if (lookup.Item?.Comments is not null)
        {
            await store.SaveCommentsAsync(videoId, lookup.Item.Comments, now, ct).ConfigureAwait(false);
            context.Count(c => c with { CommentsRead = c.CommentsRead + lookup.Item.Comments.Count });
        }

        // The index's copy again from PostgreSQL, where the refresh wrote what it read (2.0).
        UpsertResult written = await videos
            .ReindexAsync([videoId], ct)
            .ConfigureAwait(false);
        context.Count(c => Add(c, written));
        if (thumbnail?.Versions is not null)
        {
            await videos
                .SetThumbnailArtAsync(new Dictionary<string, ArtVersions> { [videoId] = thumbnail.Versions }, ct)
                .ConfigureAwait(false);
        }

        return null;
    }

    /// <param name="counted">
    /// Its answer counts with the run's (found, gone, to retry); else it's a note of the run (a channel refreshed with
    /// its videos: the counts are its videos').
    /// </param>
    /// <returns>What YouTube said when it refused (nothing is recorded then); null otherwise.</returns>
    private async Task<string?> RefreshChannelAsync(string channelId, RunContext context, bool counted)
    {
        CancellationToken ct = context.CancellationToken;
        Channel? channel = await channels.GetAsync(channelId, ct).ConfigureAwait(false);
        if (channel is null)
        {
            return null;
        }

        YouTubeLookup<YouTubeChannel> lookup = await youTube.GetChannelAsync(channelId, ct).ConfigureAwait(false);
        if (lookup.Answer == YouTubeAnswer.Refused)
        {
            return lookup.Reason ?? "YouTube refused";
        }

        await RecordChannelAsync(channel, lookup, context, counted).ConfigureAwait(false);
        return null;
    }

    /// <summary>
    /// Records what YouTube answered of a channel of the library (not a refusal): its state, and when it's there its
    /// name, description and pictures; then its index copy.
    /// </summary>
    private async Task RecordChannelAsync(
        Channel channel,
        YouTubeLookup<YouTubeChannel> lookup,
        RunContext context,
        bool counted)
    {
        CancellationToken ct = context.CancellationToken;
        string channelId = channel.Id;
        context.Count(c => c with { ChannelsRead = c.ChannelsRead + 1 });
        ChannelFacts? known = (await facts.GetChannelsAsync([channelId], ct).ConfigureAwait(false))
            .GetValueOrDefault(channelId);
        DateTimeOffset now = clock.GetUtcNow();
        SavedPicture? avatar = null;
        SavedPicture? banner = null;
        if (lookup.Item is not null)
        {
            Picture wantedAvatar = new(
                YouTubePictures.Avatar(lookup.Item.Thumbnails),
                channel.ThumbPath,
                YouTubePictures.ChannelStem(channelId, "thumb"),
                ArtSubject.ChannelThumb);
            Picture wantedBanner = new(
                YouTubePictures.Banner(lookup.Item.Thumbnails),
                channel.BannerPath,
                YouTubePictures.ChannelStem(channelId, "banner"),
                ArtSubject.ChannelBanner);
            avatar = await SavePictureAsync(wantedAvatar, context).ConfigureAwait(false);
            banner = await SavePictureAsync(wantedBanner, context).ConfigureAwait(false);
        }

        ChannelFacts learned = lookup.Answer switch
        {
            YouTubeAnswer.Found => new ChannelFacts(
                channelId,
                new YouTubeStatus(YouTubeState.OnYouTube, now, null, now),
                RefreshedOf(lookup.Item!, channel, known?.Refreshed, avatar?.Path, banner?.Path)),
            YouTubeAnswer.Gone => new ChannelFacts(
                channelId,
                new YouTubeStatus(YouTubeState.Gone, now, lookup.Reason, known?.Status.RefreshedAt),
                known?.Refreshed),
            _ when IsConfirmed(known?.Status) => known!,
            _ => new ChannelFacts(
                channelId,
                new YouTubeStatus(YouTubeState.RetryLater, now, lookup.Reason, known?.Status.RefreshedAt),
                known?.Refreshed),
        };
        if (counted)
        {
            Counted(channelId, lookup, context);
        }
        else
        {
            Logged(channelId, lookup);
            context.Note(lookup.Answer switch
            {
                YouTubeAnswer.Found => "The channel: on YouTube.",
                YouTubeAnswer.Gone => $"The channel: gone from YouTube ({lookup.Reason}).",
                YouTubeAnswer.Private => $"The channel: private on YouTube ({lookup.Reason}).",
                _ => $"The channel: no answer to trust from YouTube ({lookup.Reason}), to retry.",
            });
        }
        if (ReferenceEquals(learned, known))
        {
            LogKept(channelId);
        }
        else
        {
            await store.SaveChannelAsync(learned, ct).ConfigureAwait(false);
        }

        UpsertResult written = await channels
            .ReindexAsync([channelId], ct)
            .ConfigureAwait(false);
        context.Count(c => Add(c, written));
        foreach ((ArtSubject subject, SavedPicture? picture) in new[]
        {
            (ArtSubject.ChannelThumb, avatar),
            (ArtSubject.ChannelBanner, banner),
        })
        {
            if (picture?.Versions is not null)
            {
                await channels
                    .SetArtAsync(subject, new Dictionary<string, ArtVersions> { [channelId] = picture.Versions }, ct)
                    .ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// "Add a channel" (2.0, B4): reads the channel on YouTube by its id or @handle, adds it to the library when it's
    /// new (its name, description and pictures recorded as a refresh records them), then subscribes to it with the
    /// run's tabs. Nothing is downloaded: the YouTube syncs (B5) do (question 14).
    /// </summary>
    /// <returns>What YouTube said when it refused (nothing is recorded then); null otherwise.</returns>
    /// <exception cref="ChannelNotAddedException">Gone, private, or no answer to trust: the run fails.</exception>
    private async Task<string?> AddChannelAsync(string target, RunContext context)
    {
        CancellationToken ct = context.CancellationToken;
        YouTubeLookup<YouTubeChannel> lookup = await youTube.GetChannelAsync(target, ct).ConfigureAwait(false);
        if (lookup.Answer == YouTubeAnswer.Refused)
        {
            return lookup.Reason ?? "YouTube refused";
        }

        if (lookup.Item is null)
        {
            Logged(target, lookup);
            throw new ChannelNotAddedException(lookup.Answer switch
            {
                YouTubeAnswer.Gone => $"Not added: {target} isn't on YouTube ({lookup.Reason}).",
                YouTubeAnswer.Private => $"Not added: {target} is private on YouTube ({lookup.Reason}).",
                _ => $"Not added: no answer to trust from YouTube ({lookup.Reason}); try again later.",
            });
        }

        // A handle becomes the channel's id: the run links to the channel's page.
        context.Update(r => r with { Target = lookup.Item.Id });
        Channel? channel = await channels.GetAsync(lookup.Item.Id, ct).ConfigureAwait(false);
        if (channel is null)
        {
            channel = new Channel(
                lookup.Item.Id,
                lookup.Item.Name,
                "",
                0,
                [],
                null,
                null,
                null,
                YouTubeStatus.NotChecked);
            UpsertResult added = await channels
                .AddMissingAsync([channel], LibraryOrigin.TonTuyau, ct)
                .ConfigureAwait(false);
            context.Count(c => Add(c, added));
        }

        await RecordChannelAsync(channel, lookup, context, counted: true).ConfigureAwait(false);
        // Named when it was asked for (the settings' default then); a run from before B6 without any: the three.
        ChannelTabs tabs = context.Run.Tabs ?? ChannelTabs.All;
        ChannelSubscription? had = await subscriptions.GetAsync(lookup.Item.Id, ct).ConfigureAwait(false);
        ChannelSubscription subscription = new(
            lookup.Item.Id,
            tabs,
            clock.GetUtcNow(),
            context.Run.StartedBy,
            LibraryOrigin.TonTuyau);
        await subscriptions
            .SetAsync(subscription, ct)
            .ConfigureAwait(false);
        if (had is null)
        {
            context.Count(c => c with { Subscribed = c.Subscribed + 1 });
            context.Note(
                $"Subscribed to \"{lookup.Item.Name}\": {TabsText(tabs)}. Its videos come with the YouTube syncs.");
        }
        else
        {
            context.Note($"Subscribed to \"{lookup.Item.Name}\" already: its tabs are now {TabsText(tabs)}.");
        }

        return null;
    }

    private async Task<string> SubscribedAlreadyAsync(string channelId, CancellationToken cancellationToken)
    {
        Channel? channel = await channels.GetAsync(channelId, cancellationToken).ConfigureAwait(false);
        return $"TonTuyau is subscribed to \"{channel?.Name ?? channelId}\" already: its admin page changes its tabs.";
    }

    /// <summary>"videos, shorts and streams", "videos only"…</summary>
    private static string TabsText(ChannelTabs tabs)
    {
        string[] names =
        [
            .. new[] { (tabs.Videos, "videos"), (tabs.Shorts, "shorts"), (tabs.Streams, "streams") }
                .Where(t => t.Item1)
                .Select(t => t.Item2),
        ];
        return names switch
        {
            [] => "no tab",
            [string one] => one + " only",
            [.. string[] first, string last] => string.Join(", ", first) + " and " + last,
        };
    }

    /// <summary>
    /// Downloads a video into the media folder, then adds it to the library (with its channel first, when it's new);
    /// a video of the library whose file was missing (B3) gets its file, the rest of it refreshed as a refresh does.
    /// </summary>
    /// <returns>What YouTube said when it refused (nothing is recorded then); null otherwise.</returns>
    /// <exception cref="YouTubeDownloadException">Gone, private, or not downloaded: the run fails.</exception>
    private async Task<string?> DownloadVideoAsync(string videoId, YouTubeSettings read, RunContext context)
    {
        CancellationToken ct = context.CancellationToken;
        string folder = media.NewDownloadFolder(videoId);
        try
        {
            LogDownloading(videoId, read.DownloadFormat);
            YouTubeLookup<YouTubeDownload> lookup = await youTube
                .DownloadVideoAsync(videoId, read.DownloadFormat, folder, read.RetryWithoutCookie, ct)
                .ConfigureAwait(false);
            if (lookup.Answer == YouTubeAnswer.Refused)
            {
                return lookup.Reason ?? "YouTube refused";
            }

            if (lookup.Item is null)
            {
                throw YouTubeDownloadException.NotDownloaded(lookup.Reason ?? "yt-dlp didn't say why");
            }

            context.Count(c => c with { VideosRead = c.VideosRead + 1, FoundOnYouTube = c.FoundOnYouTube + 1 });
            context.Progressed(1);
            YouTubeVideo found = lookup.Item.Video;
            // The library's, its file missing (CheckAsync, when the run started); null: a new one.
            Video? known = await videos.GetAsync(videoId, ct).ConfigureAwait(false);
            // Its channel first: a video's channel is in the library before it.
            bool newChannel = known is null
                && await channels.GetAsync(found.ChannelId, ct).ConfigureAwait(false) is null;
            if (newChannel)
            {
                Channel channel = new(
                    found.ChannelId,
                    found.ChannelName,
                    "",
                    0,
                    [],
                    null,
                    null,
                    null,
                    YouTubeStatus.NotChecked);
                await channels
                    .AddMissingAsync([channel], LibraryOrigin.TonTuyau, ct)
                    .ConfigureAwait(false);
            }

            string mediaPath = $"{found.ChannelId}/{videoId}.mkv";
            media.Place(lookup.Item.File, mediaPath);
            LogPlaced(videoId, mediaPath, lookup.Item.Size);
            Picture wanted = new(
                YouTubePictures.Thumbnail(found.Thumbnails),
                known?.ThumbnailPath,
                YouTubePictures.VideoStem(videoId),
                ArtSubject.VideoThumbnail);
            SavedPicture? thumbnail = await SavePictureAsync(wanted, context).ConfigureAwait(false);
            DateTimeOffset now = clock.GetUtcNow();
            Video video = known is null
                ? await AddDownloadedAsync(videoId, found, lookup.Item, mediaPath, thumbnail, now, context)
                    .ConfigureAwait(false)
                : await SetFileAsync(known, lookup.Item, mediaPath, now, context).ConfigureAwait(false);
            // What YouTube said of it, as a refresh records it: its state, chapters, comment count.
            await store
                .SaveVideoAsync(
                    new VideoFacts(
                        videoId,
                        found.ChannelId,
                        new YouTubeStatus(StateOf(found), now, null, now),
                        RefreshedOf(found, video, null, thumbnail?.Path)),
                    ct)
                .ConfigureAwait(false);
            await videos
                .ReindexAsync([videoId], ct)
                .ConfigureAwait(false);
            if (thumbnail?.Versions is not null)
            {
                await videos
                    .SetThumbnailArtAsync(new Dictionary<string, ArtVersions> { [videoId] = thumbnail.Versions }, ct)
                    .ConfigureAwait(false);
            }

            if (newChannel)
            {
                // A new channel's name, description and pictures, read on YouTube as a refresh reads them.
                context.BeginPhase(SyncPhase.YouTubeChannel);
                string? refused = await RefreshChannelAsync(found.ChannelId, context, counted: false)
                    .ConfigureAwait(false);
                if (refused is not null)
                {
                    context.Note(
                        $"The new channel {found.ChannelId} wasn't read on YouTube ({refused}): refresh it later.");
                }
            }

            // It failed before (B5): it's downloaded now.
            await downloadFailures.RemoveAsync(videoId, ct).ConfigureAwait(false);
            // Its keyframes read now (C3): its first play has them.
            await streaming.PrepareAsync(videoId, ct).ConfigureAwait(false);
            return null;
        }
        finally
        {
            media.RemoveDownloadFolder(folder);
        }
    }

    /// <summary>Adds a video downloaded (its channel is in the library already).</summary>
    private async Task<Video> AddDownloadedAsync(
        string videoId,
        YouTubeVideo found,
        YouTubeDownload download,
        string mediaPath,
        SavedPicture? thumbnail,
        DateTimeOffset now,
        RunContext context)
    {
        Video video = new(
            videoId,
            found.ChannelId,
            found.ChannelName,
            found.Title,
            found.Description,
            download.Type,
            found.Published ?? now,
            now,
            TimeSpan.FromSeconds(found.DurationSeconds ?? 0),
            download.Size,
            mediaPath,
            false,
            thumbnail?.Path ?? "",
            found.Tags,
            found.Categories,
            new VideoStats(found.Views ?? 0, found.Likes ?? 0, 0),
            download.Tracks,
            YouTubeStatus.NotChecked);
        UpsertResult added = await videos
            .AddMissingAsync([video], LibraryOrigin.TonTuyau, context.CancellationToken)
            .ConfigureAwait(false);
        // By type too (alpha.8): the dashboard's history splits the new videos as the imports do.
        context.Count(c => Add(c, added) with { CreatedByType = AddType(c.CreatedByType, video.Type, added.Created) });
        return video;
    }

    /// <summary>Records the file downloaded for a video of the library whose file was missing (B3).</summary>
    private async Task<Video> SetFileAsync(
        Video known,
        YouTubeDownload download,
        string mediaPath,
        DateTimeOffset now,
        RunContext context)
    {
        VideoFile file = new(mediaPath, download.Size, now, download.Tracks);
        await videos
            .SetFileAsync(known.Id, file, context.CancellationToken)
            .ConfigureAwait(false);
        context.Count(c => c with { Updated = c.Updated + 1 });
        context.Note($"The video \"{known.Title}\" had no file: it's downloaded now, {mediaPath}.");
        return known with
        {
            MediaPath = mediaPath,
            MediaPathCorrected = false,
            MediaSize = download.Size,
            Downloaded = now,
            Tracks = download.Tracks,
        };
    }

    /// <summary>
    /// Downloads a picture into the art volume (<see cref="YouTubePictures.PathFor"/>), and makes its resized versions.
    /// </summary>
    /// <returns>Where it went and its versions; null when it couldn't be had (the current one stays).</returns>
    private async Task<SavedPicture?> SavePictureAsync(Picture wanted, RunContext context)
    {
        if (wanted.Url is null)
        {
            return null;
        }

        YouTubeImage? image = await images.GetAsync(wanted.Url, context.CancellationToken).ConfigureAwait(false);
        string? replaced = LibraryPaths.IsArtPath(wanted.Current) ? wanted.Current : null;
        string path = YouTubePictures.PathFor(replaced, wanted.Stem, image?.Extension ?? "jpg");
        if (image is null || !LibraryPaths.IsArtPath(path))
        {
            context.Count(c => c with { ArtFailed = c.ArtFailed + 1 });
            return null;
        }

        try
        {
            // The versions of the previous picture go with it.
            art.Delete(path);
            using MemoryStream content = new(image.Content);
            await art.SaveAsync(path, content, context.CancellationToken).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            LogPictureFailed(ex, path);
            context.Count(c => c with { ArtFailed = c.ArtFailed + 1 });
            return null;
        }

        if (replaced is not null && replaced != path)
        {
            // Another format than the one it replaces: the old file isn't used any more.
            art.Delete(replaced);
        }

        context.Count(c => c with { ArtCopied = c.ArtCopied + 1 });
        IReadOnlyList<int> widths = ArtSizes.For(wanted.Subject);
        try
        {
            ArtResizeResult? resized = resizer.EnsureVersions(path, widths);
            context.Count(c => c with { ArtResized = c.ArtResized + (resized?.Made ?? 0) });
            return new SavedPicture(path, resized?.Versions);
        }
        catch (ArtResizeException ex)
        {
            LogPictureFailed(ex, path);
            context.Count(c => c with { ArtResizeFailed = c.ArtResizeFailed + ex.Missing });
            return new SavedPicture(path, art.Describe(path, widths));
        }
    }

    /// <summary>YouTube's metadata; what it doesn't say (views, likes hidden) stays as known.</summary>
    private static RefreshedVideo RefreshedOf(
        YouTubeVideo item,
        Video video,
        RefreshedVideo? known,
        string? thumbnail) =>
        new(
            item.Title,
            item.ChannelName,
            item.Description,
            item.Tags,
            item.Categories,
            item.Views ?? video.Stats.Views,
            item.Likes ?? video.Stats.Likes,
            item.CommentCount ?? known?.CommentCount,
            item.Chapters,
            thumbnail ?? known?.ThumbnailPath);

    private static RefreshedChannel RefreshedOf(
        YouTubeChannel item,
        Channel channel,
        RefreshedChannel? known,
        string? thumb,
        string? banner) =>
        new(
            item.Name,
            item.Description,
            item.Subscribers ?? channel.Subscribers,
            item.Tags,
            thumb ?? known?.ThumbPath,
            banner ?? known?.BannerPath);

    private static bool IsConfirmed(YouTubeStatus? status) =>
        status?.State is YouTubeState.OnYouTube or YouTubeState.Unlisted or YouTubeState.Private or YouTubeState.Gone;

    /// <summary>A video YouTube answered: unlisted or private (its owner reading it) when yt-dlp says so.</summary>
    private static YouTubeState StateOf(YouTubeVideo found) => found.Availability switch
    {
        "unlisted" => YouTubeState.Unlisted,
        "private" => YouTubeState.Private,
        _ => YouTubeState.OnYouTube,
    };

    private void Logged<T>(string id, YouTubeLookup<T> lookup)
        where T : class
    {
        if (lookup.Answer == YouTubeAnswer.Gone)
        {
            LogGone(id, lookup.Reason);
        }
        else if (lookup.Answer == YouTubeAnswer.Private)
        {
            LogPrivate(id, lookup.Reason);
        }
        else if (lookup.Answer == YouTubeAnswer.CheckFailed)
        {
            LogCheckFailed(id, lookup.Reason);
        }
    }

    private void Counted<T>(string id, YouTubeLookup<T> lookup, RunContext context)
        where T : class
    {
        Logged(id, lookup);

        context.Count(c => lookup.Answer switch
        {
            YouTubeAnswer.Found => c with
            {
                FoundOnYouTube = c.FoundOnYouTube + 1,
                UnlistedOnYouTube = c.UnlistedOnYouTube
                    + (lookup.Item is YouTubeVideo video && StateOf(video) == YouTubeState.Unlisted ? 1 : 0),
            },
            YouTubeAnswer.Gone => c with { GoneFromYouTube = c.GoneFromYouTube + 1 },
            YouTubeAnswer.Private => c with { PrivateOnYouTube = c.PrivateOnYouTube + 1 },
            _ => c with { CheckFailed = c.CheckFailed + 1 },
        });
    }

    private static TypeCounts AddType(TypeCounts? counts, VideoType type, int created)
    {
        TypeCounts before = counts ?? TypeCounts.None;
        return type switch
        {
            VideoType.Short => before with { Shorts = before.Shorts + created },
            VideoType.Stream => before with { Streams = before.Streams + created },
            _ => before with { Videos = before.Videos + created },
        };
    }

    private static SyncCounts Add(SyncCounts counts, UpsertResult result) => counts with
    {
        Created = counts.Created + result.Created,
        Updated = counts.Updated + result.Updated,
        Unchanged = counts.Unchanged + result.Unchanged,
    };

    private static bool IsRunFailure(Exception ex) =>
        ex is SearchStoreException
            or OperationCanceledException
            or IOException
            or DbException
            or ValidationException
            or YouTubeDownloadException
            or ChannelNotAddedException
        || ex.InnerException is DbException;

    /// <summary>A progress line (<see cref="ProgressLines"/>): in the backend's log and the run's log.</summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Downloading {VideoId} (format {Format}).")]
    private partial void LogDownloading(string videoId, string format);

    [LoggerMessage(Level = LogLevel.Information, Message = "Downloaded {VideoId}: {MediaPath}, {Size} bytes.")]
    private partial void LogPlaced(string videoId, string mediaPath, long size);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Line}")]
    private partial void LogProgress(string line);

    [LoggerMessage(Level = LogLevel.Information, Message = "Refresh {RunId} started: {Mode} of {Target} by {Trigger}.")]
    private partial void LogStarted(string runId, SyncMode mode, string target, SyncTrigger trigger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Comments read: {Read} (the YouTube page's settings).")]
    private partial void LogComments(string read);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Id}: private on YouTube ({Reason}).")]
    private partial void LogPrivate(string id, string? reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Gone video, skipped for the refresh: {Id} ({Title}).")]
    private partial void LogSkippedGone(string id, string title);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Id}: gone from YouTube ({Reason}).")]
    private partial void LogGone(string id, string? reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Id}: no answer to trust from YouTube ({Reason}): to retry.")]
    private partial void LogCheckFailed(string id, string? reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Id}: what TonTuyau confirmed before stays.")]
    private partial void LogKept(string id);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The picture {Path} can't be stored: tried again next time.")]
    private partial void LogPictureFailed(Exception exception, string path);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Refresh {RunId} resumed by {By}: {Left} left to read.")]
    private partial void LogResumed(string runId, string by, int left);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Refresh {RunId} on hold: YouTube refused ({Reason}). It resumes at {ResumeAt}, {Left} left to read.")]
    private partial void LogHeld(string runId, string reason, DateTimeOffset resumeAt, int left);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Run {RunId} on hold: under the disk space threshold. It tries again at {ResumeAt}, {Left} left.")]
    private partial void LogHeldForSpace(string runId, DateTimeOffset resumeAt, int left);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Refresh {RunId} put on hold by {By}, {Left} left to read: it resumes only with Resume now.")]
    private partial void LogHeldByHand(string runId, string by, int left);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refresh {RunId} canceled by {By} while on hold.")]
    private partial void LogCanceledHeld(string runId, string by);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refresh {RunId} failed: {Reason}")]
    private partial void LogFailed(string runId, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Refresh {RunId} stopped by an unexpected error.")]
    private partial void LogUnexpected(Exception exception, string runId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Refresh {RunId} finished: {Outcome}.")]
    private partial void LogFinished(string runId, SyncOutcome outcome);

    /// <summary>A picture to store.</summary>
    /// <param name="Url">The picture YouTube offers; null when it offers none (nothing changes).</param>
    /// <param name="Current">The picture there now (TubeArchivist's copy, or a previous refresh's).</param>
    /// <param name="Stem">Its name in the art volume, without the format (<see cref="YouTubePictures.PathFor"/>).</param>
    private sealed record Picture(string? Url, string? Current, string Stem, ArtSubject Subject);

    /// <summary>A picture stored by a refresh: where, and its resized versions (null when none could be read).</summary>
    private sealed record SavedPicture(string Path, ArtVersions? Versions);
}
