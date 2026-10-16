using TonTuyau.Application.Sync;
using TonTuyau.Application.Tasks;

namespace TonTuyau.Api.Hosting;

/// <summary>
/// Resumes by itself a refresh put on hold after YouTube refused (1.5), once its cool-down is over: every 30 seconds,
/// the one due first, when no run is going on here. Another run holding the lock makes it wait for the next look.
/// </summary>
internal sealed partial class RefreshResumer(
    ITaskRepository runs,
    SyncRunner runner,
    TimeProvider clock,
    ILogger<RefreshResumer> logger) : BackgroundService
{
    public static readonly TimeSpan LookEvery = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // A timer of its own: a look that takes long doesn't push the next ones back.
        using PeriodicTimer timer = new(LookEvery, clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await ResumeDueAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The backend stops.
        }
    }

    /// <summary>Resumes the run due first, if any; anything unexpected (the index down…) is logged, and tried again.</summary>
    private async Task ResumeDueAsync(CancellationToken stoppingToken)
    {
        if (runner.RunningId is not null)
        {
            return;
        }

        try
        {
            IReadOnlyList<SyncRun> held = await runs
                .GetOnHoldRunsAsync(stoppingToken)
                .ConfigureAwait(false);
            DateTimeOffset now = clock.GetUtcNow();
            if (held.FirstOrDefault(r => r.ResumeAt <= now) is not { } due)
            {
                return;
            }

            StartedRun started = await runner
                .StartResumeAsync(due.Id, SyncTrigger.Scheduler)
                .ConfigureAwait(false);
            LogResumed(started.Id);
        }
        catch (SyncBusyException ex)
        {
            LogWaiting(ex.Message);
        }
        catch (SyncRefusedException ex)
        {
            LogWaiting(ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogUnexpected(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Refresh {RunId}: its cool-down is over, it resumes.")]
    private partial void LogResumed(string runId);

    [LoggerMessage(Level = LogLevel.Information, Message = "A refresh on hold waits: {Reason}")]
    private partial void LogWaiting(string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Resuming a refresh on hold failed.")]
    private partial void LogUnexpected(Exception exception);
}
