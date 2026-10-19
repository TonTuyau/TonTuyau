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
/// The TubeArchivist import and the index rebuild (2.0), each a run: one at a time across every process (the lock).
/// The import walks TubeArchivist's whole library and takes from it only what TonTuyau doesn't have (a channel or a
/// video TonTuyau already has is left as it is); it never deletes anything. TubeArchivist can be shut down once it ran.
/// The rebuild writes the library from PostgreSQL into a new index generation.
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
    IndexRebuildState rebuildState,
    TonTuyauOptions options,
    TimeProvider clock,
    ILogger<SyncService> logger)
{
    /// <summary>How many rows an index rebuild reads from PostgreSQL and writes to the index at once.</summary>
    public const int IndexBatchSize = 500;

    /// <summary>Why an import can't run: TubeArchivist's settings aren't there.</summary>
    public const string NoTubeArchivist =
        "TubeArchivist isn't set up (TONTUYAU_TA_URL, TONTUYAU_TA_TOKEN): there's nothing to import.";

    private SyncOptions Settings => options.Sync;

    /// <summary>Whether TubeArchivist's settings are there: without them, TonTuyau runs without it.</summary>
    public bool CanImport => options.TubeArchivist is not null;

    /// <summary>An import nobody watches (at startup).</summary>
    /// <exception cref="SyncBusyException">Another run holds the lock.</exception>
    /// <exception cref="SyncRefusedException">TubeArchivist isn't set up.</exception>
    public Task<SyncRun> ImportAsync(SyncTrigger trigger, CancellationToken cancellationToken) =>
        ImportAsync(trigger, null, NoSyncProgress.Instance, cancellationToken);

    /// <param name="startedBy">The account that started it from the Tasks page; null otherwise.</param>
    /// <param name="progress">Told where the run is as it goes (the CLI shows it).</param>
    /// <exception cref="SyncBusyException">Another run holds the lock.</exception>
    /// <exception cref="SyncRefusedException">TubeArchivist isn't set up.</exception>
    public Task<SyncRun> ImportAsync(
        SyncTrigger trigger,
        string? startedBy,
        ISyncProgressReporter progress,
        CancellationToken cancellationToken)
    {
        if (!CanImport)
        {
            throw new SyncRefusedException(NoTubeArchivist);
        }

        return WithLockAsync(SyncMode.Import, trigger, startedBy, progress, cancellationToken);
    }

    /// <summary>
    /// Writes the whole library (PostgreSQL) into a new index generation and switches users to it at the end:
    /// nothing is read from TubeArchivist, which may be down or gone.
    /// </summary>
    /// <exception cref="SyncBusyException">Another run holds the lock.</exception>
    public Task<SyncRun> RebuildAsync(SyncTrigger trigger, CancellationToken cancellationToken) =>
        RebuildAsync(trigger, NoSyncProgress.Instance, cancellationToken);

    /// <inheritdoc cref="RebuildAsync(SyncTrigger, CancellationToken)"/>
    /// <param name="progress">Told where the rebuild is as it goes (the CLI shows it).</param>
    public Task<SyncRun> RebuildAsync(
        SyncTrigger trigger,
        ISyncProgressReporter progress,
        CancellationToken cancellationToken) =>
        WithLockAsync(SyncMode.IndexRebuild, trigger, null, progress, cancellationToken);

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
            await state.GetLockAsync(cancellationToken).ConfigureAwait(false),
            await state.GetRecentRunsAsync(10, cancellationToken).ConfigureAwait(false),
            (await state.GetPendingArtAsync(cancellationToken).ConfigureAwait(false)).Count,
            indexStatus);
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

    private async Task<SyncRun> WithLockAsync(
        SyncMode mode,
        SyncTrigger trigger,
        string? startedBy,
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
                StartedBy = startedBy,
            };
            IReadOnlySet<string> pending = await state.GetPendingArtAsync(linked.Token).ConfigureAwait(false);
            using ProgressLines logged = new(LogProgress, clock);
            RunContext context = new(
                state,
                run,
                pending,
                new BothProgress(progress, logged),
                clock.GetUtcNow(),
                linked.Token);
            await context.SaveAsync().ConfigureAwait(false);
            // From here on, every line logged (from any class) is also this run's log (1.4).
            using IDisposable? scope = logger.BeginScope(new RunLogScope(run.Id));
            LogStarted(run.Id, mode, trigger);
            progress.Started(run.Id);

            try
            {
                // Indices made from an older definition (e.g. after an upgrade): rebuilt first, from PostgreSQL, then
                // the import goes on in them (QA10 M5-10).
                bool rebuild = mode == SyncMode.IndexRebuild;
                if (!rebuild && await IsRebuildPendingAsync(linked.Token).ConfigureAwait(false))
                {
                    rebuild = true;
                    context.Note("Index rebuild first: the index definitions changed since the indices were made.");
                    LogRebuildFirst(run.Id);
                }

                if (rebuild)
                {
                    await RebuildIndicesAsync(context).ConfigureAwait(false);
                }

                if (mode == SyncMode.Import)
                {
                    await ImportLibraryAsync(context).ConfigureAwait(false);
                }

                context.Update(r => r with { Outcome = SyncOutcome.Succeeded });
            }
            catch (Exception ex) when (IsRunFailure(ex))
            {
                Stopped(context, ex, lease, cancellationToken);
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

    // ---------------------------------------------------------------- index rebuild

    /// <summary>
    /// A new index generation, filled from PostgreSQL, then users switched to it. Aborted on failure: users keep the
    /// generation they had.
    /// </summary>
    private async Task RebuildIndicesAsync(RunContext context)
    {
        context.Update(r => r with { IndexRebuild = true });
        rebuildState.Status = IndexRebuildStatus.Running;
        IndexRebuild? rebuilding = null;
        try
        {
            rebuilding = await indices.BeginRebuildAsync(context.CancellationToken).ConfigureAwait(false);
            context.Note($"Index rebuild: writing to {string.Join(", ", rebuilding.NewIndices.Values)}.");
            await IndexLibraryAsync(context).ConfigureAwait(false);
            context.BeginPhase(SyncPhase.IndexSwitch);
            // Not cancellable: once users are switched to the new indices, an abort would delete them.
            await indices.CompleteRebuildAsync(rebuilding, CancellationToken.None).ConfigureAwait(false);
            rebuilding = null;
            rebuildState.Status = IndexRebuildStatus.UpToDate;
        }
        catch (Exception ex) when (IsRunFailure(ex))
        {
            if (rebuilding is not null)
            {
                await indices.AbortRebuildAsync(rebuilding, CancellationToken.None).ConfigureAwait(false);
                context.Note("Index rebuild aborted: users still see the previous generation.");
            }

            rebuildState.Status = IndexRebuildStatus.Pending;
            throw;
        }
    }

    /// <summary>Writes every channel and video of the library (PostgreSQL) to the index's write alias.</summary>
    private async Task IndexLibraryAsync(RunContext context)
    {
        CancellationToken ct = context.CancellationToken;
        long total = await channels.CountAsync(ct).ConfigureAwait(false)
            + await videos.CountAsync(ct).ConfigureAwait(false);
        context.BeginPhase(SyncPhase.Indexing, total <= int.MaxValue ? (int)total : null);
        int done = 0;
        await IndexAllAsync(channels.GetIdsAfterAsync, channels.ReindexAsync).ConfigureAwait(false);
        await IndexAllAsync(videos.GetIdsAfterAsync, videos.ReindexAsync).ConfigureAwait(false);
        await indices.RefreshAsync(ct).ConfigureAwait(false);

        async Task IndexAllAsync(
            Func<string?, int, CancellationToken, Task<IReadOnlyList<string>>> idsAfter,
            Func<IReadOnlyCollection<string>, CancellationToken, Task<UpsertResult>> reindex)
        {
            string? after = null;
            while (true)
            {
                IReadOnlyList<string> ids = await idsAfter(after, IndexBatchSize, ct).ConfigureAwait(false);
                if (ids.Count == 0)
                {
                    return;
                }

                await reindex(ids, ct).ConfigureAwait(false);
                done += ids.Count;
                context.Progressed(done);
                after = ids[^1];
            }
        }
    }

    // ---------------------------------------------------------------- import

    private async Task ImportLibraryAsync(RunContext context)
    {
        CancellationToken ct = context.CancellationToken;
        MediaRootState mediaRoot = media.CheckRoot();
        if (mediaRoot != MediaRootState.Available)
        {
            string rootState = mediaRoot.ToString().ToLowerInvariant();
            context.Note($"Media check skipped: the media root is {rootState} (share not mounted?).");
        }

        // Channels first: every channel TubeArchivist lists; the new ones are added.
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

            await ProcessChannelsAsync(page, context).ConfigureAwait(false);
            walkedChannels.AddRange(page.Items);
            channelDocuments += page.Items.Count + page.Rejected.Count;
            context.Progressed(channelDocuments);
        }

        // Then each channel's videos. Progress counts channels, whose number is known, not videos: TubeArchivist's
        // library total isn't what this walk will read (rejected, orphans).
        context.BeginPhase(SyncPhase.ChannelVideos, walkedChannels.Count);
        int channelsWalked = 0;
        foreach (Channel channel in walkedChannels)
        {
            bool typeCapped = await WalkChannelVideosAsync(channel, mediaRoot, context).ConfigureAwait(false);
            if (typeCapped)
            {
                context.Note(
                    $"{channel.Name} ({channel.Id}): a video type has more than {Settings.TaResultWindow} videos; "
                    + "only the reachable ones were imported.");
            }

            await context.SaveAsync().ConfigureAwait(false);
            context.Progressed(++channelsWalked);
        }

        await indices.RefreshAsync(ct).ConfigureAwait(false);
        if (mediaRoot == MediaRootState.Available)
        {
            context.BeginPhase(SyncPhase.MediaCheck);
            await FlagMissingMediaAsync(context).ConfigureAwait(false);
        }

        await RetryPendingArtAsync(context).ConfigureAwait(false);
    }

    /// <returns>Whether a video type had more videos than TubeArchivist's window lets read.</returns>
    private async Task<bool> WalkChannelVideosAsync(Channel channel, MediaRootState mediaRoot, RunContext context)
    {
        CancellationToken ct = context.CancellationToken;
        VideoPageQuery all = new(VideoSort.Published, SortOrder.Asc, channel.Id);
        Page<Video> first = await tubeArchivist.GetVideoPageAsync(all, 1, ct).ConfigureAwait(false);
        if (!first.TotalIsCapped)
        {
            return await WalkPagesAsync(all, first, mediaRoot, context).ConfigureAwait(false);
        }

        // More than the window in one list: walk it type by type.
        bool capped = false;
        foreach (VideoType type in Enum.GetValues<VideoType>())
        {
            VideoPageQuery query = all with { Type = type };
            Page<Video> firstOfType = await tubeArchivist.GetVideoPageAsync(query, 1, ct).ConfigureAwait(false);
            capped |= await WalkPagesAsync(query, firstOfType, mediaRoot, context).ConfigureAwait(false);
        }

        return capped;
    }

    /// <returns>Whether the walk stopped at TubeArchivist's window.</returns>
    private async Task<bool> WalkPagesAsync(
        VideoPageQuery query,
        Page<Video> first,
        MediaRootState mediaRoot,
        RunContext context)
    {
        Page<Video> page = first;
        for (int number = 1; !page.IsPastEnd; number++)
        {
            await ProcessVideosAsync(page, context, checkMedia: mediaRoot == MediaRootState.Available)
                .ConfigureAwait(false);
            if (number * page.PageSize >= Settings.TaResultWindow)
            {
                return true;
            }

            page = await tubeArchivist.GetVideoPageAsync(query, number + 1, context.CancellationToken)
                .ConfigureAwait(false);
        }

        return false;
    }

    /// <summary>Flags the imported videos whose file the walk didn't find on the media share.</summary>
    private async Task FlagMissingMediaAsync(RunContext context)
    {
        await videos.SetMediaMissingAsync([.. context.MissingMedia], true, context.CancellationToken)
            .ConfigureAwait(false);
        context.Count(c => c with { MediaMissing = context.MissingMedia.Count });
        if (context.MissingMedia.Count > 0)
        {
            string sample = string.Join(", ", context.MissingMedia.Order(StringComparer.Ordinal).Take(10));
            context.Note($"{context.MissingMedia.Count} video file(s) missing on the media share, e.g. {sample}.");
        }
    }

    // ---------------------------------------------------------------- pages

    private async Task ProcessVideosAsync(Page<Video> page, RunContext context, bool checkMedia)
    {
        CancellationToken ct = context.CancellationToken;
        int read = page.Items.Count + page.Rejected.Count;
        int corrected = page.Items.Count(v => v.MediaPathCorrected);
        context.Count(c => c with { VideosRead = c.VideosRead + read, Corrected = c.Corrected + corrected });
        LogRejected(page.Rejected);
        context.Rejected(page.Rejected);
        // Only what TonTuyau doesn't have: a video it has already is left as it is.
        UpsertResult result = await videos.AddMissingAsync(page.Items, ct).ConfigureAwait(false);
        HashSet<string> created = new(result.CreatedIds, StringComparer.Ordinal);
        Video[] added = page.Items
            .Where(v => created.Contains(v.Id))
            .DistinctBy(v => v.Id, StringComparer.Ordinal)
            .ToArray();
        context.Count(c => Add(c, result) with { CreatedByType = Add(c.CreatedByType, CountByType(added)) });
        foreach (Video video in added)
        {
            await CopyArtAsync(video.ThumbnailPath, context).ConfigureAwait(false);
            if (checkMedia)
            {
                context.CheckedFile(video.Id, media.Exists(video.MediaPath));
            }
        }

        await ResizeArtAsync([.. added.Select(v => v.ThumbnailPath)], context).ConfigureAwait(false);
    }

    private async Task ProcessChannelsAsync(Page<Channel> page, RunContext context)
    {
        CancellationToken ct = context.CancellationToken;
        context.Count(c => c with { ChannelsRead = c.ChannelsRead + page.Items.Count + page.Rejected.Count });
        LogRejected(page.Rejected);
        context.Rejected(page.Rejected);
        UpsertResult result = await channels.AddMissingAsync(page.Items, ct).ConfigureAwait(false);
        HashSet<string> created = new(result.CreatedIds, StringComparer.Ordinal);
        Channel[] added = page.Items
            .Where(c => created.Contains(c.Id))
            .DistinctBy(c => c.Id, StringComparer.Ordinal)
            .ToArray();
        foreach (Channel channel in added)
        {
            await CopyChannelArtAsync(channel, context).ConfigureAwait(false);
        }

        await ResizeArtAsync([.. added.SelectMany(ResizedArtOf)], context).ConfigureAwait(false);
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

        string reason = lockLost ? "The run lock was lost (another process holds it): stopped." : ex.Message;
        context.Update(r => r with { Outcome = SyncOutcome.Failed, Error = reason });
        LogFailed(context.Run.Id, reason);
    }

    private static bool IsRunFailure(Exception ex) =>
        ex is TubeArchivistException
            or SearchStoreException
            or OperationCanceledException
            or IOException
            or ValidationException;

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

    private static TypeCounts CountByType(IReadOnlyCollection<Video> added) =>
        new(
            added.Count(v => v.Type == VideoType.Video),
            added.Count(v => v.Type == VideoType.Short),
            added.Count(v => v.Type == VideoType.Stream));

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

    private void LogRejected(IReadOnlyList<RejectedItem> rejected)
    {
        foreach (RejectedItem item in rejected)
        {
            LogRejected(item.Subject, string.Join("; ", item.Errors.Select(e => $"{e.Field} {e.Message}")));
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Index status unavailable for the status.")]
    private partial void LogStatusIndicesFailed(Exception exception);

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
        Message = "Run {RunId}: the index definitions changed, so it rebuilds the indices first.")]
    private partial void LogRebuildFirst(string runId);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Line}")]
    private partial void LogProgress(string line);

    [LoggerMessage(Level = LogLevel.Information, Message = "Run {RunId} started: {Mode} by {Trigger}.")]
    private partial void LogStarted(string runId, SyncMode mode, SyncTrigger trigger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Run {RunId} failed: {Reason}")]
    private partial void LogFailed(string runId, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Run {RunId} finished: {Outcome}.")]
    private partial void LogFinished(string runId, SyncOutcome outcome);
}
