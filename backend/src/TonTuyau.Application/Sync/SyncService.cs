using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TonTuyau.Application.Configuration;
using TonTuyau.Application.Library;
using TonTuyau.Application.RunLogs;
using TonTuyau.Application.Store;
using TonTuyau.Application.TubeArchivist;
using TonTuyau.Application.Validation;

namespace TonTuyau.Application.Sync;

/// <summary>
/// The TubeArchivist → TonTuyau sync, as agreed in docs/SYNC_DESIGN.md (v5): incremental and full runs, deletion with
/// confirmation, <c>apply-deletions</c>, index rebuild. One run at a time across every process (the lock).
/// </summary>
public sealed partial class SyncService(
    ITubeArchivistGateway tubeArchivist,
    IVideoRepository videos,
    IChannelRepository channels,
    IIndexManager indices,
    ISyncStateStore state,
    IArtStore art,
    IArtResizer resizer,
    IMediaLibrary media,
    IDeletionLog deletionLog,
    IndexRebuildState rebuildState,
    TonTuyauOptions options,
    TimeProvider clock,
    ILogger<SyncService> logger)
{
    private SyncOptions Settings => options.Sync;

    /// <summary>A run nobody watches (the scheduler).</summary>
    /// <exception cref="SyncBusyException">Another sync holds the lock.</exception>
    public Task<SyncRun> RunAsync(SyncMode mode, SyncTrigger trigger, CancellationToken cancellationToken) =>
        RunAsync(mode, trigger, NoSyncProgress.Instance, cancellationToken);

    /// <param name="progress">Told where the run is as it goes (the CLI shows it).</param>
    /// <exception cref="SyncBusyException">Another sync holds the lock.</exception>
    public Task<SyncRun> RunAsync(
        SyncMode mode,
        SyncTrigger trigger,
        ISyncProgressReporter progress,
        CancellationToken cancellationToken) =>
        WithLockAsync(mode, trigger, rebuild: false, progress, cancellationToken);

    /// <summary>Re-imports everything into a new index generation and switches users to it at the end.</summary>
    /// <exception cref="SyncBusyException">Another sync holds the lock.</exception>
    public Task<SyncRun> RebuildAsync(SyncTrigger trigger, CancellationToken cancellationToken) =>
        RebuildAsync(trigger, NoSyncProgress.Instance, cancellationToken);

    /// <inheritdoc cref="RebuildAsync(SyncTrigger, CancellationToken)"/>
    /// <param name="progress">Told where the rebuild is as it goes (the CLI shows it).</param>
    public Task<SyncRun> RebuildAsync(
        SyncTrigger trigger,
        ISyncProgressReporter progress,
        CancellationToken cancellationToken) =>
        WithLockAsync(SyncMode.Full, trigger, rebuild: true, progress, cancellationToken);

    /// <summary>
    /// Deletes, after confirming each one with TubeArchivist, the list a blocked full run wrote. A run of its own
    /// (<see cref="SyncMode.ApplyDeletions"/>, 1.4): the full run's record stays as that run left it.
    /// </summary>
    /// <exception cref="SyncBusyException">Another sync holds the lock.</exception>
    /// <exception cref="SyncRefusedException">Not the latest full run, or no list for it.</exception>
    public Task<SyncRun> ApplyDeletionsAsync(string runId, SyncTrigger trigger, CancellationToken cancellationToken) =>
        ApplyDeletionsAsync(runId, trigger, NoSyncProgress.Instance, cancellationToken);

    /// <inheritdoc cref="ApplyDeletionsAsync(string, SyncTrigger, CancellationToken)"/>
    /// <param name="progress">Told where it is as it goes (the CLI shows it).</param>
    public async Task<SyncRun> ApplyDeletionsAsync(
        string runId,
        SyncTrigger trigger,
        ISyncProgressReporter progress,
        CancellationToken cancellationToken)
    {
        if (!LibraryPaths.IsRunId(runId))
        {
            throw new SyncRefusedException($"'{runId}' isn't a run id (expected run-YYYYMMDDTHHMMSSZ-xxxxxx).");
        }

        ISyncLease lease = await state.TryAcquireLockAsync(SyncMode.ApplyDeletions, trigger, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new SyncBusyException(await state.GetLockAsync(cancellationToken).ConfigureAwait(false));
        await using (lease.ConfigureAwait(false))
        {
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                lease.Lost);
            SyncRun? latest = await state.GetLatestFullRunAsync(linked.Token).ConfigureAwait(false);
            if (latest?.Id != runId)
            {
                throw new SyncRefusedException(
                    $"{runId} isn't the latest full run ({latest?.Id ?? "none"}): its list may be outdated. Check the latest one instead.");
            }

            IReadOnlyList<DeletionEntry> entries = await deletionLog.ReadAsync(runId, linked.Token)
                .ConfigureAwait(false)
                ?? throw new SyncRefusedException($"There's no deletion list for {runId}.");

            SyncRun run = new()
            {
                Id = SyncRun.NewId(clock.GetUtcNow()),
                Mode = SyncMode.ApplyDeletions,
                Trigger = trigger,
                StartedAt = clock.GetUtcNow(),
                AppliesTo = runId,
                DeletionsPlanned = entries.Count,
            };
            using ProgressLines logged = new(LogProgress, clock);
            RunContext context = NewContext(
                run,
                new HashSet<string>(),
                new BothProgress(progress, logged),
                linked.Token);
            await context.SaveAsync().ConfigureAwait(false);
            using IDisposable? scope = logger.BeginScope(new RunLogScope(run.Id));
            LogStarted(run.Id, run.Mode, trigger);
            progress.Started(run.Id);
            try
            {
                context.BeginPhase(SyncPhase.DeletionList, entries.Count);
                List<DeletionEntry> stillStored = new();
                int lookedUp = 0;
                foreach (DeletionEntry entry in entries)
                {
                    bool stored = entry.Kind == "channel"
                        ? await channels.GetAsync(entry.Id, linked.Token).ConfigureAwait(false) is not null
                        : await videos.GetAsync(entry.Id, linked.Token).ConfigureAwait(false) is not null;
                    if (stored)
                    {
                        stillStored.Add(entry);
                    }

                    context.Progressed(++lookedUp);
                }

                await ConfirmAndDeleteAsync(stillStored, context).ConfigureAwait(false);
                await indices.RefreshAsync(CancellationToken.None).ConfigureAwait(false);
                context.Update(r => r with { Outcome = SyncOutcome.Succeeded });
            }
            catch (Exception ex) when (IsRunFailure(ex))
            {
                // What was deleted until then stays deleted, and is counted.
                Stopped(context, ex, lease, cancellationToken);
            }
            finally
            {
                context.Update(r => r with { FinishedAt = clock.GetUtcNow() });
                await context.SaveAsync().ConfigureAwait(false);
            }

            logged.Finish();
            LogFinished(context.Run.Id, context.Run.Outcome);
            return context.Run;
        }
    }

    public async Task<SyncStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<IndexStatus> indexStatus;
        try
        {
            indexStatus = await indices.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SearchStoreException ex)
        {
            // The rest of the status is still worth showing.
            LogStatusIndicesFailed(ex);
            indexStatus = [];
        }

        return new SyncStatus(
            await state.GetCheckpointAsync(cancellationToken).ConfigureAwait(false),
            await state.GetLockAsync(cancellationToken).ConfigureAwait(false),
            await state.GetRecentRunsAsync(10, cancellationToken).ConfigureAwait(false),
            (await state.GetPendingArtAsync(cancellationToken).ConfigureAwait(false)).Count,
            indexStatus);
    }

    private async Task<SyncRun> WithLockAsync(
        SyncMode mode,
        SyncTrigger trigger,
        bool rebuild,
        ISyncProgressReporter progress,
        CancellationToken cancellationToken)
    {
        ISyncLease lease = await state.TryAcquireLockAsync(mode, trigger, cancellationToken).ConfigureAwait(false)
            ?? throw new SyncBusyException(await state.GetLockAsync(cancellationToken).ConfigureAwait(false));
        await using (lease.ConfigureAwait(false))
        {
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                lease.Lost);
            SyncRun run = new()
            {
                Id = SyncRun.NewId(clock.GetUtcNow()),
                Mode = mode,
                Trigger = trigger,
                StartedAt = clock.GetUtcNow(),
            };
            IReadOnlySet<string> pending = await state.GetPendingArtAsync(linked.Token).ConfigureAwait(false);
            using ProgressLines logged = new(LogProgress, clock);
            RunContext context = NewContext(run, pending, new BothProgress(progress, logged), linked.Token);
            await context.SaveAsync().ConfigureAwait(false);
            // From here on, every line logged (from any class) is also this run's log (1.4).
            using IDisposable? scope = logger.BeginScope(new RunLogScope(run.Id));
            LogStarted(run.Id, mode, trigger);
            progress.Started(run.Id);

            IndexRebuild? rebuilding = null;
            try
            {
                // Indices made from an older definition (e.g. after an upgrade): this run rebuilds them instead, with
                // every safety of a full run, and tries again at each run until it succeeds (QA10 M5-10).
                if (!rebuild && await IsRebuildPendingAsync(linked.Token).ConfigureAwait(false))
                {
                    rebuild = true;
                    context.Update(r => r with { Mode = SyncMode.Full });
                    context.Note(
                        $"Index rebuild instead of the {mode.ToString().ToLowerInvariant()} sync: the index "
                        + "definitions changed since the indices were made.");
                    LogRebuildInstead(run.Id, mode);
                }

                if (rebuild)
                {
                    context.Update(r => r with { IndexRebuild = true });
                    rebuildState.Status = IndexRebuildStatus.Running;
                    rebuilding = await indices.BeginRebuildAsync(linked.Token).ConfigureAwait(false);
                    context.Note($"Index rebuild: writing to {string.Join(", ", rebuilding.NewIndices.Values)}.");
                    await FullAsync(context).ConfigureAwait(false);
                    context.BeginPhase(SyncPhase.IndexSwitch);
                    // Not cancellable: once users are switched to the new indices, an abort would delete them.
                    await indices.CompleteRebuildAsync(rebuilding, CancellationToken.None).ConfigureAwait(false);
                    rebuilding = null;
                    rebuildState.Status = IndexRebuildStatus.UpToDate;
                }
                else if (mode == SyncMode.Incremental)
                {
                    await IncrementalAsync(context).ConfigureAwait(false);
                }
                else
                {
                    await FullAsync(context).ConfigureAwait(false);
                }

                context.Update(r => r with { Outcome = SyncOutcome.Succeeded });
            }
            catch (Exception ex) when (IsRunFailure(ex))
            {
                Stopped(context, ex, lease, cancellationToken);
                if (rebuilding is not null)
                {
                    await indices.AbortRebuildAsync(rebuilding, CancellationToken.None).ConfigureAwait(false);
                    context.Note("Index rebuild aborted: users still see the previous generation.");
                }

                if (context.Run.IndexRebuild)
                {
                    rebuildState.Status = IndexRebuildStatus.Pending;
                }
            }
            finally
            {
                context.Update(r => r with { FinishedAt = clock.GetUtcNow() });
                await state.SavePendingArtAsync(context.PendingArt, CancellationToken.None).ConfigureAwait(false);
                await context.SaveAsync().ConfigureAwait(false);
            }

            logged.Finish();
            LogFinished(context.Run.Id, context.Run.Outcome);
            return context.Run;
        }
    }

    /// <summary>
    /// True when an index users read was made from another definition than this TonTuyau's (the hash recorded in its
    /// mapping, M2): it needs a rebuild. Also updates <see cref="IndexRebuildState"/> (unless a rebuild is running).
    /// </summary>
    /// <exception cref="SearchStoreException">Elasticsearch can't be used, or the indices don't exist yet.</exception>
    public async Task<bool> IsRebuildPendingAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<IndexStatus> statuses = await indices.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        bool pending = statuses.Any(s => !s.DefinitionUpToDate);
        if (rebuildState.Status != IndexRebuildStatus.Running)
        {
            rebuildState.Status = pending ? IndexRebuildStatus.Pending : IndexRebuildStatus.UpToDate;
        }

        return pending;
    }

    private RunContext NewContext(
        SyncRun run,
        IReadOnlySet<string> pendingArt,
        ISyncProgressReporter progress,
        CancellationToken cancellationToken) =>
        new(
            state,
            run,
            pendingArt,
            Settings.RejectGuardPercent,
            Settings.RejectGuardMinDocuments,
            progress,
            clock.GetUtcNow(),
            cancellationToken);

    // ---------------------------------------------------------------- incremental

    private async Task IncrementalAsync(RunContext context)
    {
        CancellationToken ct = context.CancellationToken;
        SyncCheckpoint checkpoint = await state.GetCheckpointAsync(ct).ConfigureAwait(false);
        long taTotal = (await tubeArchivist.GetLibraryStatsAsync(ct).ConfigureAwait(false)).Total;
        long ours = await videos.CountAsync(ct).ConfigureAwait(false);

        string? escalation = checkpoint switch
        {
            { Watermark: null } => "no watermark yet",
            { FullRunRequested: true } => "a previous incremental run requested it",
            _ when taTotal - ours > Settings
                .TaResultWindow => $"{taTotal - ours} videos missing (> {Settings.TaResultWindow})",
            _ => null,
        };
        if (escalation is not null)
        {
            context.Update(r => r with { Mode = SyncMode.Full });
            context.Note($"Escalated to a full run: {escalation}.");
            await FullAsync(context).ConfigureAwait(false);
            return;
        }

        long floor = checkpoint.Watermark!.Value
            - (long)TimeSpan.FromHours(Settings.IncrementalMarginHours).TotalSeconds;
        VideoPageQuery query = new(VideoSort.Downloaded, SortOrder.Desc);
        // No total: where the watermark is in TubeArchivist's list is only known on reaching it.
        context.BeginPhase(SyncPhase.NewVideos);
        for (int number = 1; ; number++)
        {
            Page<Video> page = await tubeArchivist.GetVideoPageAsync(query, number, ct).ConfigureAwait(false);
            if (page.IsPastEnd)
            {
                break;
            }

            await ProcessVideosAsync(page, runId: null, context, checkMedia: false).ConfigureAwait(false);
            await context.SaveAsync().ConfigureAwait(false);
            context.Progressed(number);

            if (page.Items.Count > 0 && page.Items.Min(v => v.Downloaded.ToUnixTimeSeconds()) < floor)
            {
                break;
            }

            if (number * page.PageSize >= Settings.TaResultWindow)
            {
                // The next page would be past TubeArchivist's window (HTTP 500): stop cleanly, let a full run catch up.
                context.Note(
                    $"Reached TubeArchivist's {Settings.TaResultWindow}-result window before the watermark: "
                    + "full run requested for the next tick.");
                await state.SaveCheckpointAsync(checkpoint with { FullRunRequested = true }, ct).ConfigureAwait(false);
                return;
            }
        }

        await RetryPendingArtAsync(context).ConfigureAwait(false);
        await SaveWatermarkAsync(checkpoint, context).ConfigureAwait(false);
    }

    // ---------------------------------------------------------------- full

    private async Task FullAsync(RunContext context)
    {
        CancellationToken ct = context.CancellationToken;
        string runId = context.Run.Id;
        SyncCheckpoint checkpoint = await state.GetCheckpointAsync(ct).ConfigureAwait(false);
        MediaRootState mediaRoot = media.CheckRoot();
        if (mediaRoot != MediaRootState.Available)
        {
            string rootState = mediaRoot.ToString().ToLowerInvariant();
            context.Note($"Media check skipped: the media root is {rootState} (share not mounted?).");
        }

        // Channels first: every channel TubeArchivist lists, marked as seen by this run.
        long channelCount = await tubeArchivist.GetChannelCountAsync(ct).ConfigureAwait(false);
        // TubeArchivist's channel count is exact (its stats, not a capped hit total).
        context.BeginPhase(SyncPhase.Channels, channelCount <= int.MaxValue ? (int)channelCount : null);
        List<Channel> walkedChannels = new();
        int channelDocuments = 0;
        for (int number = 1; ; number++)
        {
            Page<Channel> page = await tubeArchivist.GetChannelPageAsync(number, ct).ConfigureAwait(false);
            if (page.IsPastEnd)
            {
                break;
            }

            await ProcessChannelsAsync(page, runId, context).ConfigureAwait(false);
            walkedChannels.AddRange(page.Items);
            channelDocuments += page.Items.Count + page.Rejected.Count;
            context.Progressed(channelDocuments);
        }

        bool channelsComplete = channelDocuments == channelCount;
        if (!channelsComplete)
        {
            context.Note(
                $"Channel list incomplete ({channelDocuments} walked, TubeArchivist counts {channelCount}): "
                + "no channel is deleted this run.");
        }

        // Then each channel's videos; deletion is decided per channel. Progress counts channels, whose number is
        // known, not videos: TubeArchivist's library total isn't what this walk will read (rejected, orphans).
        context.BeginPhase(SyncPhase.ChannelVideos, walkedChannels.Count);
        List<string> deletable = new();
        int channelsWalked = 0;
        foreach (Channel channel in walkedChannels)
        {
            (int walked, long expected, bool typeCapped) = await WalkChannelVideosAsync(
                channel,
                runId,
                mediaRoot,
                context)
                .ConfigureAwait(false);
            if (typeCapped)
            {
                context.Note(
                    $"{channel.Name} ({channel.Id}): a video type has more than {Settings.TaResultWindow} videos; "
                    + "only the reachable ones were imported, nothing is deleted in this channel.");
            }
            else if (walked != expected)
            {
                context.Note(
                    $"{channel.Name} ({channel.Id}): walked {walked} videos, TubeArchivist counts {expected}; "
                    + "nothing is deleted in this channel.");
            }
            else
            {
                deletable.Add(channel.Id);
            }

            await context.SaveAsync().ConfigureAwait(false);
            context.Progressed(++channelsWalked);
        }

        await indices.RefreshAsync(ct).ConfigureAwait(false);
        if (mediaRoot == MediaRootState.Available)
        {
            context.BeginPhase(SyncPhase.MediaCheck);
            await UpdateMediaFlagsAsync(context).ConfigureAwait(false);
        }

        await PlanAndDeleteAsync(deletable, channelsComplete, context).ConfigureAwait(false);
        await RetryPendingArtAsync(context).ConfigureAwait(false);
        await SaveWatermarkAsync(checkpoint with { FullRunRequested = false }, context).ConfigureAwait(false);
    }

    private async Task<(int Walked, long Expected, bool TypeCapped)> WalkChannelVideosAsync(
        Channel channel,
        string runId,
        MediaRootState mediaRoot,
        RunContext context)
    {
        CancellationToken ct = context.CancellationToken;
        VideoPageQuery all = new(VideoSort.Published, SortOrder.Asc, channel.Id);
        Page<Video> first = await tubeArchivist.GetVideoPageAsync(all, 1, ct).ConfigureAwait(false);
        if (!first.TotalIsCapped)
        {
            (int Documents, bool ReachedWindow) walked = await WalkPagesAsync(all, first, runId, mediaRoot, context)
                .ConfigureAwait(false);
            return (walked.Documents, first.TotalHits, false);
        }

        // More than the window in one list: walk it type by type, against the channel's true total.
        long expected = await tubeArchivist.GetChannelVideoTotalAsync(channel.Id, ct).ConfigureAwait(false) ?? 0;
        int total = 0;
        bool capped = false;
        foreach (VideoType type in Enum.GetValues<VideoType>())
        {
            VideoPageQuery query = all with { Type = type };
            Page<Video> firstOfType = await tubeArchivist.GetVideoPageAsync(query, 1, ct).ConfigureAwait(false);
            (int Documents, bool ReachedWindow) walked =
                await WalkPagesAsync(query, firstOfType, runId, mediaRoot, context).ConfigureAwait(false);
            total += walked.Documents;
            capped |= walked.ReachedWindow;
        }

        return (total, expected, capped);
    }

    private async Task<(int Documents, bool ReachedWindow)> WalkPagesAsync(
        VideoPageQuery query,
        Page<Video> first,
        string runId,
        MediaRootState mediaRoot,
        RunContext context)
    {
        int documents = 0;
        Page<Video> page = first;
        for (int number = 1; !page.IsPastEnd; number++)
        {
            await ProcessVideosAsync(page, runId, context, checkMedia: mediaRoot == MediaRootState.Available)
                .ConfigureAwait(false);
            documents += page.Items.Count + page.Rejected.Count;
            if (number * page.PageSize >= Settings.TaResultWindow)
            {
                return (documents, true);
            }

            page = await tubeArchivist.GetVideoPageAsync(query, number + 1, context.CancellationToken)
                .ConfigureAwait(false);
        }

        return (documents, false);
    }

    private async Task UpdateMediaFlagsAsync(RunContext context)
    {
        CancellationToken ct = context.CancellationToken;
        IReadOnlySet<string> flagged = await videos.GetMediaMissingIdsAsync(ct).ConfigureAwait(false);
        string[] newlyMissing = context.MissingMedia
            .Where(id => !flagged.Contains(id))
            .ToArray();
        string[] found = flagged
            .Where(id => !context.MissingMedia.Contains(id))
            .ToArray();
        await videos.SetMediaMissingAsync(newlyMissing, true, ct).ConfigureAwait(false);
        await videos.SetMediaMissingAsync(found, false, ct).ConfigureAwait(false);
        context.Count(c => c with { MediaMissing = context.MissingMedia.Count });
        if (context.MissingMedia.Count > 0)
        {
            string sample = string.Join(", ", context.MissingMedia.Order(StringComparer.Ordinal).Take(10));
            context.Note($"{context.MissingMedia.Count} video file(s) missing on the media share, e.g. {sample}.");
        }
    }

    // ---------------------------------------------------------------- deletion

    private async Task PlanAndDeleteAsync(List<string> deletableChannels, bool channelsComplete, RunContext context)
    {
        CancellationToken ct = context.CancellationToken;
        string runId = context.Run.Id;
        IReadOnlyList<VideoSummary> staleVideos = deletableChannels.Count > 0
            ? await videos.FindNotSeenAsync(runId, deletableChannels, ct).ConfigureAwait(false)
            : [];
        IReadOnlyList<ChannelSummary> goneChannels = channelsComplete
            ? await channels.FindNotSeenAsync(runId, ct).ConfigureAwait(false)
            : [];
        IReadOnlyList<VideoSummary> goneChannelVideos = goneChannels.Count > 0
            ? await videos.FindByChannelsAsync(goneChannels.Select(c => c.Id).ToArray(), ct).ConfigureAwait(false)
            : [];

        DeletionEntry[] entries = staleVideos
            .Concat(goneChannelVideos)
            .DistinctBy(v => v.Id, StringComparer.Ordinal)
            .Select(v => new DeletionEntry(
                "video",
                v.Id,
                v.Title,
                v.ChannelId,
                v.Type.ToString().ToLowerInvariant(),
                v.Downloaded,
                v.SeenInRun)
            { Art = [v.ThumbnailPath] })
            .Concat(goneChannels.Select(
                c => new DeletionEntry("channel", c.Id, c.Name, null, null, null, c.SeenInRun) { Art = c.ArtPaths }))
            .ToArray();

        string file = await deletionLog.WriteAsync(runId, entries, ct).ConfigureAwait(false);
        context.Update(r => r with { DeletionFile = file, DeletionsPlanned = entries.Length });
        if (entries.Length > Settings.MaxDeletions)
        {
            context.BeginPhase(SyncPhase.DeletionsOverLimit, entries.Length);
            context.Update(r => r with { DeletionsBlockedByLimit = true });
            context.Note(
                $"""
                {entries.Length} deletions planned > limit {Settings.MaxDeletions}: nothing deleted.
                Check {file}, then run: tontuyau sync apply-deletions {runId}
                """);
            return;
        }

        await ConfirmAndDeleteAsync(entries, context).ConfigureAwait(false);
    }

    /// <summary>Deletes each item only after TubeArchivist confirms it doesn't have it any more (404).</summary>
    private async Task ConfirmAndDeleteAsync(IReadOnlyList<DeletionEntry> entries, RunContext context)
    {
        CancellationToken ct = context.CancellationToken;
        context.BeginPhase(SyncPhase.Deletions, entries.Count);
        int confirmed = 0;
        foreach (DeletionEntry entry in entries)
        {
            bool stillInTubeArchivist;
            try
            {
                stillInTubeArchivist = entry.Kind == "channel"
                    ? await tubeArchivist.GetChannelAsync(entry.Id, ct).ConfigureAwait(false) is not null
                    : await tubeArchivist.GetVideoAsync(entry.Id, ct).ConfigureAwait(false) is not null;
            }
            catch (ValidationException ex)
            {
                // It exists, just broken: never delete on that basis.
                LogConfirmBroken(ex, entry.Kind, entry.Id);
                stillInTubeArchivist = true;
            }
            catch (TubeArchivistException ex)
            {
                LogConfirmStopped(ex, entry.Kind, entry.Id);
                context.Note($"Stopped deleting ({ex.Message}); the rest waits for the next run.");
                return;
            }

            if (stillInTubeArchivist)
            {
                context.Count(c => c with { KeptStillInTubeArchivist = c.KeptStillInTubeArchivist + 1 });
                context.Note(
                    $"Kept {entry.Kind} {entry.Id} ({entry.Title}): the walk missed it but TubeArchivist still has it.");
                context.Progressed(++confirmed);
                continue;
            }

            if (entry.Kind == "channel")
            {
                await channels.DeleteAsync([entry.Id], ct).ConfigureAwait(false);
            }
            else
            {
                await videos.DeleteAsync([entry.Id], ct).ConfigureAwait(false);
            }

            foreach (string? path in entry.Art.Where(LibraryPaths.IsArtPath))
            {
                art.Delete(path);
            }

            context.Count(c => c with { Deleted = c.Deleted + 1 });
            context.Progressed(++confirmed);
        }
    }

    // ---------------------------------------------------------------- pages

    private async Task ProcessVideosAsync(Page<Video> page, string? runId, RunContext context, bool checkMedia)
    {
        CancellationToken ct = context.CancellationToken;
        int read = page.Items.Count + page.Rejected.Count;
        int corrected = page.Items.Count(v => v.MediaPathCorrected);
        context.Count(c => c with { VideosRead = c.VideosRead + read, Corrected = c.Corrected + corrected });
        LogRejected(page.Rejected);
        context.Rejected(page.Rejected);

        UpsertResult result = await videos.UpsertAsync(page.Items, runId, ct).ConfigureAwait(false);
        TypeCounts createdByType = CountByType(page.Items, result.CreatedIds);
        context.Count(c => Add(c, result) with { CreatedByType = Add(c.CreatedByType, createdByType) });
        if (runId is not null)
        {
            await MarkRejectedSeenAsync(page.Rejected, id => videos.MarkSeenAsync(id, runId, ct)).ConfigureAwait(false);
        }

        foreach (Video video in page.Items)
        {
            context.SawDownloaded(video.Downloaded);
            await CopyArtAsync(video.ThumbnailPath, context).ConfigureAwait(false);
            if (checkMedia)
            {
                context.CheckedFile(video.Id, media.Exists(video.MediaPath));
            }

            if (runId is null)
            {
                await EnsureChannelKnownAsync(video.ChannelId, context).ConfigureAwait(false);
            }
        }

        await ResizeArtAsync([.. page.Items.Select(v => v.ThumbnailPath)], context).ConfigureAwait(false);
    }

    private async Task ProcessChannelsAsync(Page<Channel> page, string runId, RunContext context)
    {
        CancellationToken ct = context.CancellationToken;
        context.Count(c => c with { ChannelsRead = c.ChannelsRead + page.Items.Count + page.Rejected.Count });
        LogRejected(page.Rejected);
        context.Rejected(page.Rejected);
        await channels.UpsertAsync(page.Items, runId, ct).ConfigureAwait(false);
        await MarkRejectedSeenAsync(page.Rejected, id => channels.MarkSeenAsync(id, runId, ct)).ConfigureAwait(false);
        foreach (Channel channel in page.Items)
        {
            context.KnownChannels.Add(channel.Id);
            await CopyChannelArtAsync(channel, context).ConfigureAwait(false);
        }

        await ResizeArtAsync([.. page.Items.SelectMany(ResizedArtOf)], context).ConfigureAwait(false);
    }

    /// <summary>Incremental runs meet channels they've never seen (a new subscription): fetch them on the fly.</summary>
    private async Task EnsureChannelKnownAsync(string channelId, RunContext context)
    {
        if (!context.KnownChannels.Add(channelId))
        {
            return;
        }

        CancellationToken ct = context.CancellationToken;
        if (await channels.GetAsync(channelId, ct).ConfigureAwait(false) is not null)
        {
            return;
        }

        try
        {
            if (await tubeArchivist.GetChannelAsync(channelId, ct).ConfigureAwait(false) is { } channel)
            {
                await channels.UpsertAsync([channel], null, ct).ConfigureAwait(false);
                context.Count(c => c with { ChannelsRead = c.ChannelsRead + 1 });
                await CopyChannelArtAsync(channel, context).ConfigureAwait(false);
                await ResizeArtAsync([.. ResizedArtOf(channel)], context).ConfigureAwait(false);
            }
        }
        catch (ValidationException ex)
        {
            LogRejected(ex.Subject, ex.Message);
            context.Rejected([new RejectedItem(channelId, ex.Subject, ex.Errors)]);
        }
    }

    /// <summary>
    /// Records how a run that threw ended: Canceled when its caller asked it to stop (<paramref name="caller"/>), else
    /// Failed and why. Whatever the exception then: a request cut short doesn't always say it was canceled (the
    /// Elasticsearch client reports it as "can't be reached"). Losing the lock to another process is a failure, even
    /// when the caller stops too.
    /// </summary>
    private void Stopped(RunContext context, Exception ex, ISyncLease lease, CancellationToken caller)
    {
        bool lockLost = lease.Lost.IsCancellationRequested;
        if (caller.IsCancellationRequested && !lockLost)
        {
            context.Update(r => r with { Outcome = SyncOutcome.Canceled });
            return;
        }

        string reason = lockLost ? "The sync lock was lost (another process holds it): stopped." : ex.Message;
        context.Update(r => r with { Outcome = SyncOutcome.Failed, Error = reason });
        LogFailed(context.Run.Id, reason);
    }

    private static bool IsRunFailure(Exception ex) =>
        ex is TubeArchivistException
            or SearchStoreException
            or TubeArchivistFormatChangedException
            or OperationCanceledException
            or IOException
            or ValidationException;

    private static async Task MarkRejectedSeenAsync(IReadOnlyList<RejectedItem> rejected, Func<string[], Task> markSeen)
    {
        string[] ids = rejected
            .Select(r => r.Id)
            .OfType<string>()
            .ToArray();
        if (ids.Length > 0)
        {
            // Keeps the last good copy: a document that broke in TA must not look deleted.
            await markSeen(ids).ConfigureAwait(false);
        }
    }

    // ---------------------------------------------------------------- art

    private async Task CopyChannelArtAsync(Channel channel, RunContext context)
    {
        foreach (string? path in new[] { channel.ThumbPath, channel.BannerPath, channel.TvArtPath })
        {
            if (path is not null)
            {
                await CopyArtAsync(path, context).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Copies art that isn't in TonTuyau's art volume yet. Failures are remembered and retried, never fatal.</summary>
    private async Task CopyArtAsync(string path, RunContext context)
    {
        if (art.Exists(path))
        {
            context.PendingArt.Remove(path);
            return;
        }

        try
        {
            Stream? image = await tubeArchivist.OpenCacheFileAsync(path, context.CancellationToken)
                .ConfigureAwait(false);
            if (image is null)
            {
                ArtFailed(path, context);
                return;
            }

            await using (image.ConfigureAwait(false))
            {
                await art.SaveAsync(path, image, context.CancellationToken).ConfigureAwait(false);
            }

            context.PendingArt.Remove(path);
            context.Count(c => c with { ArtCopied = c.ArtCopied + 1 });
        }
        catch (Exception ex) when (ex is TubeArchivistException or IOException)
        {
            // Art never stops a run: it's retried at the next one.
            LogArtFailed(ex, path);
            ArtFailed(path, context);
        }
    }

    private static SyncCounts Add(SyncCounts counts, UpsertResult result) => counts with
    {
        Created = counts.Created + result.Created,
        Updated = counts.Updated + result.Updated,
        Unchanged = counts.Unchanged + result.Unchanged,
    };

    private static TypeCounts CountByType(IEnumerable<Video> items, IReadOnlyList<string> ids)
    {
        HashSet<string> wanted = new(ids, StringComparer.Ordinal);
        // The same id twice in a page was written once (the last one wins).
        Video[] matching = items
            .Where(v => wanted.Contains(v.Id))
            .GroupBy(v => v.Id, StringComparer.Ordinal)
            .Select(g => g.Last())
            .ToArray();
        return new TypeCounts(
            matching.Count(v => v.Type == VideoType.Video),
            matching.Count(v => v.Type == VideoType.Short),
            matching.Count(v => v.Type == VideoType.Stream));
    }

    private static TypeCounts Add(TypeCounts? counts, TypeCounts more) =>
        counts is null
            ? more
            : new TypeCounts(counts.Videos + more.Videos, counts.Shorts + more.Shorts, counts.Streams + more.Streams);

    private static void ArtFailed(string path, RunContext context)
    {
        context.PendingArt.Add(path);
        context.Count(c => c with { ArtFailed = c.ArtFailed + 1 });
    }

    private async Task RetryPendingArtAsync(RunContext context)
    {
        List<string> retried = [];
        string[] pending = context.PendingArt.ToArray();
        int retries = pending.Count(LibraryPaths.IsArtPath);
        if (retries > 0)
        {
            context.BeginPhase(SyncPhase.ArtRetry, retries);
        }

        foreach (string? path in pending)
        {
            if (LibraryPaths.IsArtPath(path))
            {
                await CopyArtAsync(path, context).ConfigureAwait(false);
                retried.Add(path);
                context.Progressed(retried.Count);
            }
            else
            {
                context.PendingArt.Remove(path);
            }
        }

        await ResizeArtAsync(retried, context).ConfigureAwait(false);
    }

    // ---------------------------------------------------------------- resized art (QA09)

    private static IEnumerable<string> ResizedArtOf(Channel channel) =>
        new[] { channel.ThumbPath, channel.BannerPath }.OfType<string>();

    /// <summary>
    /// Makes the missing resized versions of these (copied) images, several at once, then records what each image has
    /// in its document (so the API never looks at the disk). Images not copied are skipped; a failure is logged and
    /// counted, never fatal: the versions are tried again at the next run.
    /// </summary>
    private async Task ResizeArtAsync(IReadOnlyCollection<string> paths, RunContext context)
    {
        (string Path, ArtOwner Owner)[] jobs = paths
            .Distinct(StringComparer.Ordinal)
            .Select(p => (Path: p, Owner: LibraryPaths.OwnerOf(p)))
            .Where(j => j.Owner is not null && ArtSizes.For(j.Owner.Subject).Count > 0)
            .Select(j => (j.Path, j.Owner!))
            .ToArray();
        if (jobs.Length == 0)
        {
            return;
        }

        ConcurrentBag<(ArtOwner Owner, ArtVersions Versions)> recorded = [];
        ParallelOptions parallel = new()
        {
            MaxDegreeOfParallelism = Settings.ArtResizeParallelism,
            CancellationToken = context.CancellationToken,
        };
        await Parallel.ForEachAsync(
            jobs,
            parallel,
            (job, _) =>
            {
                IReadOnlyList<int> widths = ArtSizes.For(job.Owner.Subject);
                try
                {
                    ArtResizeResult? result = resizer.EnsureVersions(job.Path, widths);
                    if (result is not null)
                    {
                        // Counted per image (thread-safe), so the progress moves while a page's images are resized.
                        context.Count(c => c with { ArtResized = c.ArtResized + result.Made });
                        recorded.Add((job.Owner, result.Versions));
                    }
                }
                catch (ArtResizeException ex)
                {
                    // Resized art never stops a run: what exists is still recorded, the rest is retried next run.
                    LogArtResizeFailed(ex, job.Path);
                    context.Count(c => c with { ArtResizeFailed = c.ArtResizeFailed + ex.Missing });
                    if (art.Describe(job.Path, widths) is { } existing)
                    {
                        recorded.Add((job.Owner, existing));
                    }
                }

                return ValueTask.CompletedTask;
            })
            .ConfigureAwait(false);

        await RecordArtAsync(recorded, context).ConfigureAwait(false);
    }

    private async Task RecordArtAsync(
        IEnumerable<(ArtOwner Owner, ArtVersions Versions)> recorded,
        RunContext context)
    {
        CancellationToken ct = context.CancellationToken;
        IEnumerable<IGrouping<ArtSubject, (ArtOwner Owner, ArtVersions Versions)>> subjects = recorded
            .GroupBy(r => r.Owner.Subject);
        foreach (IGrouping<ArtSubject, (ArtOwner Owner, ArtVersions Versions)> group in subjects)
        {
            Dictionary<string, ArtVersions> byId = group
                .GroupBy(r => r.Owner.Id, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Last().Versions, StringComparer.Ordinal);
            if (group.Key == ArtSubject.VideoThumbnail)
            {
                await videos.SetThumbnailArtAsync(byId, ct).ConfigureAwait(false);
            }
            else
            {
                await channels.SetArtAsync(group.Key, byId, ct).ConfigureAwait(false);
            }
        }
    }

    private async Task SaveWatermarkAsync(SyncCheckpoint checkpoint, RunContext context)
    {
        long? watermark = context.MaxDownloaded is not null
            ? Math.Max(checkpoint.Watermark ?? context.MaxDownloaded.Value, context.MaxDownloaded.Value)
            : checkpoint.Watermark;
        await state.SaveCheckpointAsync(checkpoint with { Watermark = watermark }, context.CancellationToken)
            .ConfigureAwait(false);
    }

    private void LogRejected(IReadOnlyList<RejectedItem> rejected)
    {
        foreach (RejectedItem item in rejected)
        {
            LogRejected(item.Subject, string.Join("; ", item.Errors.Select(e => $"{e.Field} {e.Message}")));
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Index status unavailable for sync status.")]
    private partial void LogStatusIndicesFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "TubeArchivist sends {Kind} {Id} broken: kept, not deleted.")]
    private partial void LogConfirmBroken(Exception exception, string kind, string id);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Couldn't confirm the deletion of {Kind} {Id} with TubeArchivist: deletions stopped.")]
    private partial void LogConfirmStopped(Exception exception, string kind, string id);

    [LoggerMessage(Level = LogLevel.Error, Message = "Rejected {Subject}: {Errors}")]
    private partial void LogRejected(string subject, string errors);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Couldn't make the resized versions of {Path}: retried at the next run.")]
    private partial void LogArtResizeFailed(Exception exception, string path);

    [LoggerMessage(Level = LogLevel.Error, Message = "Couldn't copy art {Path}: retried at the next run.")]
    private partial void LogArtFailed(Exception exception, string path);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Sync {RunId}: the index definitions changed, so this {Mode} run rebuilds the indices instead.")]
    private partial void LogRebuildInstead(string runId, SyncMode mode);

    /// <summary>A progress line (<see cref="ProgressLines"/>): in the backend's log and the run's log.</summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "{Line}")]
    private partial void LogProgress(string line);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sync {RunId} started: {Mode} by {Trigger}.")]
    private partial void LogStarted(string runId, SyncMode mode, SyncTrigger trigger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sync {RunId} failed: {Reason}")]
    private partial void LogFailed(string runId, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sync {RunId} finished: {Outcome}.")]
    private partial void LogFinished(string runId, SyncOutcome outcome);
}
