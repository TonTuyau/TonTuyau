using Microsoft.Extensions.Logging;
using TonTuyau.Application.RunLogs;

namespace TonTuyau.Application.Sync;

/// <summary>
/// Starts the runs of this backend in the background (the scheduled ones; in 1.4 the Tasks page's and the command
/// line's) and knows the one going on: one at a time (the sync lock, across processes), cancellable by its id while it
/// runs, canceled when the backend stops (<see cref="StopAsync"/>). The startup index rebuild isn't one of them.
/// It owns nothing to dispose: each run's cancellation source is disposed when the run ends.
/// </summary>
public sealed partial class SyncRunner(SyncService sync, ILogger<SyncRunner> logger)
{
    private readonly Lock _gate = new();
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

    /// <summary>Starts a run, and returns once it has started (its record exists): it goes on by itself.</summary>
    /// <param name="appliesTo">For <see cref="SyncMode.ApplyDeletions"/>: the full run whose list to apply.</param>
    /// <exception cref="SyncBusyException">Another run holds the lock.</exception>
    /// <exception cref="SyncRefusedException">The list can't be applied (see <see cref="SyncService"/>).</exception>
    /// <exception cref="OperationCanceledException">The backend is stopping.</exception>
    public async Task<StartedRun> StartAsync(SyncMode mode, SyncTrigger trigger, string? appliesTo = null)
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

            run = Task.Run(() => RunInBackgroundAsync(mode, trigger, appliesTo, starting, cancel));
            _latest = run;
        }

        if (await Task.WhenAny(starting.Id, run).ConfigureAwait(false) == run)
        {
            // Over before it started: the lock was busy or the list refused (thrown here), or the backend stopped.
            SyncRun ended = await run.ConfigureAwait(false);
            return new StartedRun(ended.Id, run);
        }

        return new StartedRun(await starting.Id.ConfigureAwait(false), run);
    }

    /// <summary>Starts a run and waits for its end (the scheduler).</summary>
    /// <param name="cancellationToken">Stops the waiting, not the run (<see cref="Cancel"/> does).</param>
    /// <exception cref="SyncBusyException">Another run holds the lock.</exception>
    public async Task<SyncRun> RunAsync(SyncMode mode, SyncTrigger trigger, CancellationToken cancellationToken)
    {
        StartedRun started = await StartAsync(mode, trigger).ConfigureAwait(false);
        return await started.Finished
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
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
        SyncMode mode,
        SyncTrigger trigger,
        string? appliesTo,
        Starting starting,
        CancellationTokenSource cancel)
    {
        try
        {
            Task<SyncRun> run = mode == SyncMode.ApplyDeletions
                ? sync.ApplyDeletionsAsync(
                    appliesTo ?? throw new ArgumentNullException(nameof(appliesTo)),
                    trigger,
                    starting,
                    cancel.Token)
                : sync.RunAsync(mode, trigger, starting, cancel.Token);
            return await run.ConfigureAwait(false);
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
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sync {RunId}: canceled by {By}.")]
    private partial void LogCancel(string runId, string by);

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
