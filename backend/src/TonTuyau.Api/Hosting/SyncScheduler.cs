using TonTuyau.Application.Configuration;
using TonTuyau.Application.Sync;

namespace TonTuyau.Api.Hosting;

/// <summary>
/// Runs the syncs at the times of <see cref="SyncSchedule"/>, inside the backend. A run already going on
/// (e.g. started from the CLI) makes the tick skip; it never queues.
/// </summary>
internal sealed partial class SyncScheduler(
    SyncSchedule schedule,
    Func<SyncMode, CancellationToken, Task<SyncRun>> run,
    TonTuyauOptions options,
    TimeProvider clock,
    ILogger<SyncScheduler> logger) : BackgroundService
{
    /// <summary>The longest single wait (timers refuse more than about 49 days).</summary>
    public static readonly TimeSpan LongestWait = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Sync.SchedulerEnabled)
        {
            LogDisabled();
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            DateTimeOffset now = clock.GetUtcNow();
            if (schedule.Next(now) is not { } next)
            {
                LogNoMoreRuns();
                return;
            }

            LogNext(next.Mode, next.At);
            try
            {
                // A timer can't wait more than ~49 days (e.g. a yearly cron): wait at most a day, then look again.
                TimeSpan wait = next.At - now;
                if (wait > LongestWait)
                {
                    await Task.Delay(LongestWait, clock, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                await Task.Delay(wait, clock, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                SyncRun result = await run(next.Mode, stoppingToken).ConfigureAwait(false);
                LogRan(result.Id, result.Mode, result.Outcome);
            }
            catch (SyncBusyException ex)
            {
                LogSkipped(next.Mode, ex.Message);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Sync scheduler disabled (TONTUYAU_SYNC_SCHEDULER_ENABLED=false).")]
    private partial void LogDisabled();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sync schedule has no next occurrence: scheduler stops.")]
    private partial void LogNoMoreRuns();

    [LoggerMessage(Level = LogLevel.Information, Message = "Next scheduled sync: {Mode} at {At:u}.")]
    private partial void LogNext(SyncMode mode, DateTimeOffset at);

    [LoggerMessage(Level = LogLevel.Information, Message = "Scheduled sync {RunId} ({Mode}): {Outcome}.")]
    private partial void LogRan(string runId, SyncMode mode, SyncOutcome outcome);

    [LoggerMessage(Level = LogLevel.Information, Message = "Scheduled {Mode} sync skipped: {Reason}")]
    private partial void LogSkipped(SyncMode mode, string reason);
}
