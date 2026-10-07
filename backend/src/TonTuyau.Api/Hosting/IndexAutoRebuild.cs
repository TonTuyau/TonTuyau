using TonTuyau.Application.Configuration;
using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;

namespace TonTuyau.Api.Hosting;

/// <summary>
/// After an upgrade whose index definitions changed, rebuilds the indices once the backend has started (QA10 M5-10),
/// in the background: the site keeps serving the current generation meanwhile, readiness stays up. It's a sync like
/// the others (same lock, TubeArchivist read-only, every safety of a full run), so it only starts when the scheduler
/// is on: a backend with the scheduler off never calls TubeArchivist by itself. If it fails, each scheduled or CLI
/// sync rebuilds instead of its normal run until it succeeds.
/// </summary>
internal sealed partial class IndexAutoRebuild(
    SyncService sync,
    TonTuyauOptions options,
    TimeProvider clock,
    ILogger<IndexAutoRebuild> logger) : BackgroundService
{
    /// <summary>How long to wait between checks while Elasticsearch or the indices aren't there yet.</summary>
    public static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Never holds up startup: the checks below go to the network.
        await Task.Yield();
        bool? pending = await CheckAsync(stoppingToken).ConfigureAwait(false);
        if (pending != true)
        {
            return;
        }

        if (!options.Sync.SchedulerEnabled)
        {
            LogNotStarted();
            return;
        }

        LogStarting();
        try
        {
            SyncRun run = await sync.RebuildAsync(SyncTrigger.Startup, stoppingToken).ConfigureAwait(false);
            if (run.Outcome == SyncOutcome.Succeeded)
            {
                LogDone(run.Id);
            }
            else
            {
                LogFailed(run.Id, run.Error ?? "unknown error");
            }
        }
        catch (SyncBusyException ex)
        {
            // The run holding the lock checked the definitions when it started: it rebuilds instead, or the next does.
            LogBusy(ex.Message);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    /// <returns>Whether a rebuild is pending; null when the backend stops before Elasticsearch could be asked.</returns>
    private async Task<bool?> CheckAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                return await sync.IsRebuildPendingAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (SearchStoreException ex)
            {
                LogRetry(ex.Message, RetryDelay.TotalSeconds);
            }

            try
            {
                await Task.Delay(RetryDelay, clock, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        return null;
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The index definitions changed: rebuilding the indices now, in the background (users keep the "
            + "current ones meanwhile).")]
    private partial void LogStarting();

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Index rebuild {RunId} done: users now read the new indices.")]
    private partial void LogDone(string runId);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Index rebuild {RunId} failed ({Reason}): users keep the current indices; the next sync tries again.")]
    private partial void LogFailed(string runId, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Index rebuild not started now: {Reason}")]
    private partial void LogBusy(string reason);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = """
            The index definitions changed and the indices need a rebuild, but the scheduler is off
            (TONTUYAU_SYNC_SCHEDULER_ENABLED=false), so it isn't started automatically.
            Run `tontuyau index rebuild` (any `tontuyau sync` run rebuilds instead, too).
            """)]
    private partial void LogNotStarted();

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Index check not possible yet ({Reason}); retrying in {Seconds} s.")]
    private partial void LogRetry(string reason, double seconds);
}
