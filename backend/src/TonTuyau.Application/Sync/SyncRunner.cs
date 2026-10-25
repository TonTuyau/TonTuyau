using System.Data.Common;
using Microsoft.Extensions.Logging;
using TonTuyau.Application.Library;
using TonTuyau.Application.RunLogs;
using TonTuyau.Application.Store;
using TonTuyau.Application.Tasks;
using TonTuyau.Application.YouTube;

namespace TonTuyau.Application.Sync;

/// <summary>
/// Starts the runs of this backend in the background (the Tasks page's, the command line's, the import at startup)
/// and knows the one going on: one at a time (the run lock, across processes), cancellable by its id while it runs,
/// canceled when the backend stops (<see cref="StopAsync"/>). The startup index rebuild isn't one of them.
/// </summary>
/// <remarks>
/// The run queue (2.0): a run asked for is recorded <c>queued</c> at once, then started in order, first come first
/// served, when the lock is free and no run is on hold (YouTube refused one, or an admin held it: the queue waits
/// with it). The next one is looked for when one is asked for, when a run of this backend ends, and every few
/// seconds (<see cref="TryStartNextAsync"/>, for the lock another process or the startup rebuild let go). Resuming a
/// run on hold doesn't wait in the queue: it goes first. Each run's cancellation source is disposed when it ends.
/// </remarks>
public sealed partial class SyncRunner(
    SyncService sync,
    YouTubeRefreshService refresh,
    ISyncStateStore state,
    ITaskRepository runs,
    IYouTubeSettingsRepository settings,
    TimeProvider clock,
    ILogger<SyncRunner> logger) : IDisposable
{
    private readonly Lock _gate = new();

    /// <summary>One at a time looks for the next queued run, and cancels a queued one: never both at once.</summary>
    private readonly SemaphoreSlim _dispatching = new(1, 1);

    /// <summary>The ends of the queued runs asked for in this backend, by id.</summary>
    private readonly Dictionary<string, TaskCompletionSource<SyncRun>> _waiting = new(StringComparer.Ordinal);

    private Running? _current;
    private Task _latest = Task.CompletedTask;
    private bool _stopping;

    /// <summary>The id of the run going on in this backend; null when none is (or another process runs one).</summary>
    public string? RunningId
    {
        get
        {
            lock (_gate)
            {
                return _current?.Id;
            }
        }
    }

    /// <summary>
    /// Asks for the TubeArchivist import: queued, and started at once when nothing else goes on. Returns once it's
    /// recorded; it goes on by itself.
    /// </summary>
    /// <param name="startedBy">The account starting it from the Tasks page; null otherwise.</param>
    /// <exception cref="SyncRefusedException">TubeArchivist isn't set up.</exception>
    /// <exception cref="OperationCanceledException">The backend is stopping.</exception>
    public Task<StartedRun> StartImportAsync(SyncTrigger trigger, string? startedBy = null)
    {
        if (!sync.CanImport)
        {
            throw new SyncRefusedException(SyncService.NoTubeArchivist);
        }

        return QueueAsync(SyncMode.Import, null, trigger, startedBy);
    }

    /// <summary>Asks for an index rebuild (<c>tontuyau index rebuild</c>), a run like the others.</summary>
    /// <exception cref="OperationCanceledException">The backend is stopping.</exception>
    public Task<StartedRun> StartRebuildAsync(SyncTrigger trigger) =>
        QueueAsync(SyncMode.IndexRebuild, null, trigger, null);

    /// <summary>
    /// Asks for a refresh on YouTube (1.5: a video, a channel, or a channel and its videos) or a download (2.0).
    /// </summary>
    /// <param name="target">The video's or channel's YouTube id.</param>
    /// <param name="startedBy">The account starting it from the admin pages; null otherwise.</param>
    /// <param name="downloadFormat">A download (2.2): yt-dlp's format this once; null: the YouTube settings' one.</param>
    /// <exception cref="SyncRefusedException">Not one of the library (a refresh), one of it already (a download).</exception>
    /// <exception cref="OperationCanceledException">The backend is stopping.</exception>
    public async Task<StartedRun> StartRefreshAsync(
        SyncMode mode,
        string target,
        SyncTrigger trigger,
        string? startedBy = null,
        string? downloadFormat = null)
    {
        if (downloadFormat is not null && mode != SyncMode.DownloadVideo)
        {
            throw new ArgumentException("Only a download takes a format.", nameof(downloadFormat));
        }

        await refresh.CheckAsync(mode, target, CancellationToken.None).ConfigureAwait(false);
        return await QueueAsync(mode, target, trigger, startedBy, downloadFormat: downloadFormat).ConfigureAwait(false);
    }

    /// <summary>
    /// Asks for "Add a channel" (2.0, B4): the channel read on YouTube, added when it's new, and subscribed with these
    /// tabs.
    /// </summary>
    /// <param name="target">The channel's id or <c>@handle</c>.</param>
    /// <param name="tabs">Its tabs; null: the YouTube settings' default ones (B6, the three unless set).</param>
    /// <param name="startedBy">The account starting it from the admin pages; null otherwise.</param>
    /// <exception cref="SyncRefusedException">Not a channel's id or handle, or one TonTuyau is subscribed to.</exception>
    /// <exception cref="OperationCanceledException">The backend is stopping.</exception>
    public async Task<StartedRun> StartAddChannelAsync(
        string target,
        ChannelTabs? tabs,
        SyncTrigger trigger,
        string? startedBy = null)
    {
        await refresh.CheckAsync(SyncMode.AddChannel, target, CancellationToken.None).ConfigureAwait(false);
        ChannelTabs named = tabs
            ?? (await settings.GetAsync(CancellationToken.None).ConfigureAwait(false)).NewChannelTabs;
        return await QueueAsync(SyncMode.AddChannel, target, trigger, startedBy, named).ConfigureAwait(false);
    }

    /// <summary>
    /// Asks for a YouTube sync (2.0, B5): every subscribed channel listed on YouTube, what's new downloaded.
    /// </summary>
    /// <param name="full">The full sync: every video of their tabs, and the files missing; else the incremental one.</param>
    /// <param name="startedBy">The account starting it from the Tasks page; null otherwise.</param>
    /// <param name="channelId">
    /// The full sync of this channel alone ("Download everything" on its page); null: of every subscribed channel.
    /// </param>
    /// <exception cref="SyncRefusedException">
    /// TonTuyau isn't subscribed to that channel; or the disk space is under the threshold, set to wait (2.2.0).
    /// </exception>
    /// <exception cref="OperationCanceledException">The backend is stopping.</exception>
    public async Task<StartedRun> StartYouTubeSyncAsync(
        bool full,
        SyncTrigger trigger,
        string? startedBy = null,
        string? channelId = null)
    {
        SyncMode mode = full ? SyncMode.YouTubeFullSync : SyncMode.YouTubeSync;
        if (channelId is not null && !full)
        {
            throw new ArgumentException("Only the full sync is of one channel.", nameof(channelId));
        }

        await refresh.CheckAsync(mode, channelId ?? "", CancellationToken.None).ConfigureAwait(false);

        return await QueueAsync(mode, channelId, trigger, startedBy).ConfigureAwait(false);
    }

    /// <summary>
    /// Asks for "Refresh everything" (2.0, B8): every channel and video read on YouTube, the least recently read first.
    /// </summary>
    /// <param name="skipReadDays">What was read in the last this many days is skipped; 0: nothing is.</param>
    /// <param name="startedBy">The account starting it from the Tasks page; null otherwise.</param>
    /// <exception cref="OperationCanceledException">The backend is stopping.</exception>
    public Task<StartedRun> StartRefreshAllAsync(int skipReadDays, SyncTrigger trigger, string? startedBy = null) =>
        QueueAsync(SyncMode.RefreshAll, null, trigger, startedBy, skipReadDays: skipReadDays);

    /// <summary>Resumes a refresh on hold (1.5): the same run, from where YouTube refused it.</summary>
    /// <param name="by">The account asking; null: by itself, after the cool-down.</param>
    /// <exception cref="SyncBusyException">Another run holds the lock.</exception>
    /// <exception cref="SyncRefusedException">No such run, or it isn't on hold.</exception>
    /// <exception cref="OperationCanceledException">The backend is stopping.</exception>
    public Task<StartedRun> StartResumeAsync(string runId, SyncTrigger trigger, string? by = null) =>
        StartAsync((progress, token) => refresh.ResumeAsync(runId, trigger, by, progress, token));

    /// <summary>
    /// Starts the next queued run when this backend runs none, the lock is free, and no run is on hold; then the next
    /// one again when that one was over at once (canceled meanwhile, refused).
    /// </summary>
    public async Task TryStartNextAsync()
    {
        await _dispatching.WaitAsync().ConfigureAwait(false);
        try
        {
            while (await NextAsync().ConfigureAwait(false) is { } next)
            {
                StartedRun started;
                try
                {
                    started = await StartAsync(WorkOf(next), next.Id).ConfigureAwait(false);
                }
                catch (SyncBusyException)
                {
                    // Another process took the lock meanwhile: it stays queued, tried again later.
                    return;
                }

                if (!started.Finished.IsCompleted)
                {
                    // It runs: its end looks for the next one.
                    return;
                }
            }
        }
        catch (Exception ex) when (ex is DbException or SearchStoreException or InvalidOperationException)
        {
            // The database or the index can't be read right now: the next look tries again.
            LogDispatchFailed(ex);
        }
        finally
        {
            _dispatching.Release();
        }
    }

    /// <summary>
    /// Cancels a run while it waits in the queue: it won't start; recorded as canceled, before it started.
    /// </summary>
    /// <param name="by">Who asks, for its notes.</param>
    /// <returns>The run canceled; null when it isn't queued (any more).</returns>
    public async Task<SyncRun?> CancelQueuedAsync(string runId, string by, CancellationToken cancellationToken)
    {
        await _dispatching.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SyncRun? queued = await state.GetRunAsync(runId, cancellationToken).ConfigureAwait(false);
            if (queued?.Outcome != SyncOutcome.Queued)
            {
                return null;
            }

            SyncRun canceled = queued with
            {
                Outcome = SyncOutcome.Canceled,
                FinishedAt = clock.GetUtcNow(),
                Notes = [.. queued.Notes, $"Canceled by {by} while it waited in the queue: it didn't start."],
            };
            await state.SaveRunAsync(canceled, cancellationToken).ConfigureAwait(false);
            using (logger.BeginScope(new RunLogScope(runId)))
            {
                LogCanceledQueued(runId, by);
            }

            Ended(runId, canceled);
            return canceled;
        }
        finally
        {
            _dispatching.Release();
        }
    }

    /// <summary>Records the run asked for as queued, then starts it if its turn has come.</summary>
    private async Task<StartedRun> QueueAsync(
        SyncMode mode,
        string? target,
        SyncTrigger trigger,
        string? startedBy,
        ChannelTabs? tabs = null,
        int? skipReadDays = null,
        string? downloadFormat = null)
    {
        TaskCompletionSource<SyncRun> finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        DateTimeOffset now = clock.GetUtcNow();
        SyncRun queued = new()
        {
            Id = SyncRun.NewId(now),
            Mode = mode,
            Trigger = trigger,
            StartedAt = now,
            QueuedAt = now,
            StartedBy = startedBy,
            Target = target,
            Tabs = tabs,
            SkipReadDays = skipReadDays,
            DownloadFormat = downloadFormat,
            Outcome = SyncOutcome.Queued,
        };
        lock (_gate)
        {
            if (_stopping)
            {
                throw new OperationCanceledException("The backend is stopping: no run starts any more.");
            }

            _waiting[queued.Id] = finished;
        }

        await state.SaveRunAsync(queued, CancellationToken.None).ConfigureAwait(false);
        using (logger.BeginScope(new RunLogScope(queued.Id)))
        {
            LogQueued(queued.Id, mode, trigger);
        }

        await TryStartNextAsync().ConfigureAwait(false);
        return new StartedRun(queued.Id, finished.Task);
    }

    /// <returns>The first queued run, when its turn has come; null otherwise.</returns>
    private async Task<SyncRun?> NextAsync()
    {
        lock (_gate)
        {
            if (_stopping || _current is not null)
            {
                return null;
            }
        }

        // A lease run out (a crashed process) is free: the run starting takes it over.
        if (await state.GetLockAsync(CancellationToken.None).ConfigureAwait(false) is { } held
                && held.LeaseUntil > clock.GetUtcNow()
            || (await runs.GetOnHoldRunsAsync(CancellationToken.None).ConfigureAwait(false)).Count > 0)
        {
            return null;
        }

        IReadOnlyList<SyncRun> queue = await runs.GetQueuedRunsAsync(CancellationToken.None).ConfigureAwait(false);
        return queue.Count > 0 ? queue[0] : null;
    }

    private Func<ISyncProgressReporter, CancellationToken, Task<SyncRun>> WorkOf(SyncRun queued) =>
        queued.Mode is SyncMode.Import or SyncMode.IndexRebuild
            ? (progress, token) => sync.RunQueuedAsync(queued, progress, token)
            : (progress, token) => refresh.RunQueuedAsync(queued, progress, token);

    /// <summary>A queued run is over (or canceled before it started): whoever waits for it is told.</summary>
    private void Ended(string runId, SyncRun run)
    {
        TaskCompletionSource<SyncRun>? waiter;
        lock (_gate)
        {
            if (_waiting.Remove(runId, out waiter) && run.Outcome == SyncOutcome.Queued)
            {
                // Not over: still waiting (another process held the lock).
                _waiting[runId] = waiter;
                waiter = null;
            }
        }

        waiter?.TrySetResult(run);
    }

    /// <summary>A queued run threw: whoever waits for it gets the exception.</summary>
    private void Failed(string runId, Exception error)
    {
        TaskCompletionSource<SyncRun>? waiter;
        lock (_gate)
        {
            _waiting.Remove(runId, out waiter);
        }

        waiter?.TrySetException(error);
    }

    private async Task<StartedRun> StartAsync(
        Func<ISyncProgressReporter, CancellationToken, Task<SyncRun>> work,
        string? queuedId = null)
    {
        // The run's own: disposed when it ends (RunInBackgroundAsync).
        CancellationTokenSource cancel = new();
        Starting starting = new(this, cancel);
        Task<SyncRun> run;
        lock (_gate)
        {
            if (_stopping)
            {
                cancel.Dispose();
                throw new OperationCanceledException("The backend is stopping: no run starts any more.");
            }

            run = Task.Run(() => RunInBackgroundAsync(work, starting, cancel, queuedId));
            _latest = run;
        }

        if (await Task.WhenAny(starting.Id, run).ConfigureAwait(false) == run)
        {
            // Over before it started: the lock was busy (thrown here), or the backend stopped.
            SyncRun ended = await run.ConfigureAwait(false);
            return new StartedRun(ended.Id, run);
        }

        return new StartedRun(await starting.Id.ConfigureAwait(false), run);
    }

    /// <summary>
    /// Asks the run to stop: it ends as soon as it can (between two requests to TubeArchivist), recorded as canceled,
    /// and what it did stays. The run's log says who asked.
    /// </summary>
    /// <param name="by">Who asks, for the log: an account's name, "the command line"…</param>
    /// <returns>False when that run isn't going on in this backend (over, or another process's).</returns>
    public bool Cancel(string runId, string by)
    {
        lock (_gate)
        {
            if (_current?.Id != runId)
            {
                return false;
            }

            using (logger.BeginScope(new RunLogScope(runId)))
            {
                LogCancel(runId, by);
            }

            // Not Cancel(): it could run the run's continuations right here, under the lock, up to its end disposing
            // this very source. CancelAsync runs them elsewhere.
            _ = _current.Cancel.CancelAsync();
            return true;
        }
    }

    /// <summary>
    /// The backend stops: no run starts any more, the one going on is canceled (or as soon as it starts), and this
    /// waits for it to record its end.
    /// </summary>
    public async Task StopAsync()
    {
        Task latest;
        lock (_gate)
        {
            _stopping = true;
            latest = _latest;
            // See Cancel: not Cancel(), under the lock.
            _ = _current?.Cancel.CancelAsync();
        }

        // Its outcome (or why it didn't start) went to whoever started it: only its end matters here.
        await Task
            .WhenAny(latest)
            .ConfigureAwait(false);
    }

    private async Task<SyncRun> RunInBackgroundAsync(
        Func<ISyncProgressReporter, CancellationToken, Task<SyncRun>> work,
        Starting starting,
        CancellationTokenSource cancel,
        string? queuedId)
    {
        bool ran = false;
        try
        {
            SyncRun ended = await work(starting, cancel.Token).ConfigureAwait(false);
            if (queuedId is not null)
            {
                Ended(queuedId, ended);
            }

            ran = starting.Id.IsCompleted;
            return ended;
        }
        catch (Exception ex) when (queuedId is not null && ex is not SyncBusyException)
        {
            // Not busy (then it stays queued): whoever waits for it is told why it ended.
            ran = starting.Id.IsCompleted;
            Failed(queuedId, ex);
            throw;
        }
        finally
        {
            lock (_gate)
            {
                if (_current?.Cancel == cancel)
                {
                    _current = null;
                }
            }

            cancel.Dispose();
            if (ran)
            {
                // Its end lets the next queued run start (not awaited: this run's end doesn't wait for the next).
                _ = Task.Run(TryStartNextAsync);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Run {RunId}: canceled by {By}.")]
    private partial void LogCancel(string runId, string by);

    [LoggerMessage(Level = LogLevel.Information, Message = "Run {RunId} queued: {Mode} by {Trigger}.")]
    private partial void LogQueued(string runId, SyncMode mode, SyncTrigger trigger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Run {RunId}: canceled by {By} while it waited in the queue.")]
    private partial void LogCanceledQueued(string runId, string by);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The run queue can't be looked at right now.")]
    private partial void LogDispatchFailed(Exception error);

    public void Dispose() => _dispatching.Dispose();

    private sealed record Running(string Id, CancellationTokenSource Cancel);

    /// <summary>The run's reporter: nobody watches its progress here, but its start makes it the current run.</summary>
    private sealed class Starting(SyncRunner runner, CancellationTokenSource cancel) : ISyncProgressReporter
    {
        private readonly TaskCompletionSource<string> _id = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<string> Id => _id.Task;

        public void Report(SyncProgress progress)
        {
        }

        public void Started(string runId)
        {
            lock (runner._gate)
            {
                runner._current = new Running(runId, cancel);
                if (runner._stopping)
                {
                    // The backend began to stop while this run was taking the lock.
                    _ = cancel.CancelAsync();
                }
            }

            _id.TrySetResult(runId);
        }
    }
}
