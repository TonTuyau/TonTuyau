using System.Data.Common;
using Microsoft.Extensions.Logging;
using TonTuyau.Application.Library;
using TonTuyau.Application.RunLogs;
using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;
using TonTuyau.Application.Validation;

namespace TonTuyau.Application.YouTube;

/// <summary>
/// Reads videos and channels of the library on YouTube (1.5), as runs like the syncs (the same lock, history, log):
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
/// A video's chapters are read with it; its comments too when the YouTube settings say so (up to
/// <see cref="CommentsKept"/>, answers included, YouTube's top comments first), replacing the ones read before. Its
/// thumbnail, a channel's avatar and banner, go to TonTuyau's art volume, replacing TubeArchivist's copies; a picture
/// that fails never fails the refresh (the one there stays).
/// </remarks>
public sealed partial class YouTubeRefreshService(
    IYouTubeClient youTube,
    IYouTubeFactsSource facts,
    IYouTubeFactsStore store,
    IYouTubeSettingsRepository settings,
    IYouTubeImages images,
    IArtStore art,
    IArtResizer resizer,
    IVideoRepository videos,
    IChannelRepository channels,
    ISyncStateStore state,
    TimeProvider clock,
    ILogger<YouTubeRefreshService> logger)
{
    /// <summary>How many comments of a video TonTuyau reads and keeps, answers included (1.5).</summary>
    public const int CommentsKept = 500;

    /// <param name="mode">A refresh: <see cref="SyncMode.RefreshVideo"/>, <see cref="SyncMode.RefreshChannel"/> or
    /// <see cref="SyncMode.RefreshChannelVideos"/>.</param>
    /// <param name="target">The video's or channel's YouTube id: one of the library.</param>
    /// <param name="startedBy">The account that started it from the admin pages; null otherwise.</param>
    /// <exception cref="SyncRefusedException">Not an id, or not one of the library.</exception>
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
        await RefuseUnknownAsync(mode, target, cancellationToken).ConfigureAwait(false);
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
                Target = target,
            };
            using ProgressLines logged = new(LogProgress, clock);
            RunContext context = new(
                state,
                run,
                new HashSet<string>(),
                guardPercent: 100,
                guardMinimum: 0,
                new BothProgress(progress, logged),
                clock.GetUtcNow(),
                linked.Token);
            await context.SaveAsync().ConfigureAwait(false);
            using IDisposable? scope = logger.BeginScope(new RunLogScope(run.Id));
            LogStarted(run.Id, mode, target, trigger);
            progress.Started(run.Id);
            try
            {
                // Read once: a change on the YouTube page applies from the next run.
                YouTubeCommentsRequest? comments = (await settings.GetAsync(linked.Token).ConfigureAwait(false))
                    .ReadComments
                    ? new YouTubeCommentsRequest(CommentsKept)
                    : null;
                LogComments(comments is not null);
                if (mode == SyncMode.RefreshVideo)
                {
                    context.BeginPhase(SyncPhase.YouTubeVideos, 1);
                    await RefreshVideoAsync(target, comments, context).ConfigureAwait(false);
                    context.Progressed(1);
                }
                else
                {
                    context.BeginPhase(SyncPhase.YouTubeChannel);
                    await RefreshChannelAsync(target, context).ConfigureAwait(false);
                    if (mode == SyncMode.RefreshChannelVideos)
                    {
                        await RefreshVideosOfAsync(target, comments, context).ConfigureAwait(false);
                    }
                }

                context.Update(r => r with { Outcome = SyncOutcome.Succeeded });
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
                        ? "The sync lock was lost (another process holds it): stopped."
                        : ex.Message;
                    context.Update(r => r with { Outcome = SyncOutcome.Failed, Error = reason });
                    LogFailed(run.Id, reason);
                }
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

    private async Task RefuseUnknownAsync(SyncMode mode, string target, CancellationToken cancellationToken)
    {
        if (mode == SyncMode.RefreshVideo)
        {
            if (!LibraryIds.IsVideoId(target)
                || await videos.GetAsync(target, cancellationToken).ConfigureAwait(false) is null)
            {
                throw new SyncRefusedException($"No video {target} in the library.");
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

    private async Task RefreshVideosOfAsync(string channelId, YouTubeCommentsRequest? comments, RunContext context)
    {
        CancellationToken ct = context.CancellationToken;
        IReadOnlyList<VideoSummary> ofChannel = await videos
            .FindByChannelsAsync([channelId], ct)
            .ConfigureAwait(false);
        context.BeginPhase(SyncPhase.YouTubeVideos, ofChannel.Count);
        int done = 0;
        foreach (VideoSummary video in ofChannel.OrderByDescending(v => v.Downloaded))
        {
            ct.ThrowIfCancellationRequested();
            await RefreshVideoAsync(video.Id, comments, context).ConfigureAwait(false);
            context.Progressed(++done);
        }
    }

    /// <param name="comments">Its comments too (the YouTube settings); null: not read, those read before stay.</param>
    private async Task RefreshVideoAsync(string videoId, YouTubeCommentsRequest? comments, RunContext context)
    {
        CancellationToken ct = context.CancellationToken;
        // Read again: a video deleted since the run started is skipped.
        Video? video = await videos.GetAsync(videoId, ct).ConfigureAwait(false);
        if (video is null)
        {
            return;
        }

        YouTubeLookup<YouTubeVideo> lookup = await youTube
            .GetVideoAsync(videoId, comments, ct)
            .ConfigureAwait(false);
        context.Count(c => c with { VideosRead = c.VideosRead + 1 });
        VideoFacts? known = (await facts.GetVideosAsync([videoId], ct).ConfigureAwait(false))
            .GetValueOrDefault(videoId);
        DateTimeOffset now = clock.GetUtcNow();
        SavedPicture? thumbnail = null;
        if (lookup.Item is { } found)
        {
            Picture wanted = new(
                YouTubePictures.Thumbnail(found.Thumbnails),
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
                new YouTubeStatus(YouTubeState.OnYouTube, now, null, now),
                RefreshedOf(lookup.Item!, video, known?.Refreshed, thumbnail?.Path)),
            YouTubeAnswer.Gone => new VideoFacts(
                videoId,
                video.ChannelId,
                new YouTubeStatus(YouTubeState.Gone, now, lookup.Reason, known?.Status.RefreshedAt),
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
        if (lookup.Item?.Comments is { } read)
        {
            await store.SaveCommentsAsync(videoId, read, now, ct).ConfigureAwait(false);
            context.Count(c => c with { CommentsRead = c.CommentsRead + read.Count });
        }

        UpsertResult written = await videos
            .UpsertAsync([YouTubeOverlay.On(video, learned)], runId: null, ct)
            .ConfigureAwait(false);
        context.Count(c => Add(c, written));
        if (thumbnail?.Versions is { } versions)
        {
            await videos
                .SetThumbnailArtAsync(new Dictionary<string, ArtVersions> { [videoId] = versions }, ct)
                .ConfigureAwait(false);
        }
    }

    private async Task RefreshChannelAsync(string channelId, RunContext context)
    {
        CancellationToken ct = context.CancellationToken;
        Channel? channel = await channels.GetAsync(channelId, ct).ConfigureAwait(false);
        if (channel is null)
        {
            return;
        }

        YouTubeLookup<YouTubeChannel> lookup = await youTube.GetChannelAsync(channelId, ct).ConfigureAwait(false);
        context.Count(c => c with { ChannelsRead = c.ChannelsRead + 1 });
        ChannelFacts? known = (await facts.GetChannelsAsync([channelId], ct).ConfigureAwait(false))
            .GetValueOrDefault(channelId);
        DateTimeOffset now = clock.GetUtcNow();
        SavedPicture? avatar = null;
        SavedPicture? banner = null;
        if (lookup.Item is { } found)
        {
            Picture wantedAvatar = new(
                YouTubePictures.Avatar(found.Thumbnails),
                channel.ThumbPath,
                YouTubePictures.ChannelStem(channelId, "thumb"),
                ArtSubject.ChannelThumb);
            Picture wantedBanner = new(
                YouTubePictures.Banner(found.Thumbnails),
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
        Counted(channelId, lookup, context);
        if (ReferenceEquals(learned, known))
        {
            LogKept(channelId);
        }
        else
        {
            await store.SaveChannelAsync(learned, ct).ConfigureAwait(false);
        }

        UpsertResult written = await channels
            .UpsertAsync([YouTubeOverlay.On(channel, learned)], runId: null, ct)
            .ConfigureAwait(false);
        context.Count(c => Add(c, written));
        foreach ((ArtSubject subject, SavedPicture? picture) in new[]
        {
            (ArtSubject.ChannelThumb, avatar),
            (ArtSubject.ChannelBanner, banner),
        })
        {
            if (picture?.Versions is { } versions)
            {
                await channels
                    .SetArtAsync(subject, new Dictionary<string, ArtVersions> { [channelId] = versions }, ct)
                    .ConfigureAwait(false);
            }
        }
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
        status?.State is YouTubeState.OnYouTube or YouTubeState.Gone;

    private void Counted<T>(string id, YouTubeLookup<T> lookup, RunContext context)
        where T : class
    {
        if (lookup.Answer == YouTubeAnswer.Gone)
        {
            LogGone(id, lookup.Reason);
        }
        else if (lookup.Answer == YouTubeAnswer.CheckFailed)
        {
            LogCheckFailed(id, lookup.Reason);
        }

        context.Count(c => lookup.Answer switch
        {
            YouTubeAnswer.Found => c with { FoundOnYouTube = c.FoundOnYouTube + 1 },
            YouTubeAnswer.Gone => c with { GoneFromYouTube = c.GoneFromYouTube + 1 },
            _ => c with { CheckFailed = c.CheckFailed + 1 },
        });
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
        || ex.InnerException is DbException;

    /// <summary>A progress line (<see cref="ProgressLines"/>): in the backend's log and the run's log.</summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "{Line}")]
    private partial void LogProgress(string line);

    [LoggerMessage(Level = LogLevel.Information, Message = "Refresh {RunId} started: {Mode} of {Target} by {Trigger}.")]
    private partial void LogStarted(string runId, SyncMode mode, string target, SyncTrigger trigger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Comments read: {Read} (the YouTube page's settings).")]
    private partial void LogComments(bool read);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Id}: gone from YouTube ({Reason}).")]
    private partial void LogGone(string id, string? reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Id}: no answer to trust from YouTube ({Reason}): to retry.")]
    private partial void LogCheckFailed(string id, string? reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Id}: what TonTuyau confirmed before stays.")]
    private partial void LogKept(string id);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The picture {Path} can't be stored: tried again next time.")]
    private partial void LogPictureFailed(Exception exception, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refresh {RunId} failed: {Reason}")]
    private partial void LogFailed(string runId, string reason);

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
