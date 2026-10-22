using System.Data.Common;
using TonTuyau.Application.Store;
using TonTuyau.Application.YouTube;

namespace TonTuyau.Api.Hosting;

/// <summary>
/// Asks for the incremental YouTube sync when it's due (2.0, B5, <see cref="YouTubeSyncSchedule"/>): a look every
/// minute, the first one a minute after the backend starts (the import at startup is in the queue first).
/// </summary>
internal sealed partial class YouTubeSyncScheduler(
    YouTubeSyncSchedule schedule,
    TimeProvider clock,
    ILogger<YouTubeSyncScheduler> logger) : BackgroundService
{
    public static readonly TimeSpan Every = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(Every, clock, stoppingToken).ConfigureAwait(false);
                try
                {
                    if (await schedule.QueueIfDueAsync(stoppingToken).ConfigureAwait(false) is { } asked)
                    {
                        LogAsked(asked.Id);
                    }
                }
                catch (Exception ex) when (ex is DbException or SearchStoreException or InvalidOperationException)
                {
                    // PostgreSQL can't be read right now: the next look tries again.
                    LogLookFailed(ex);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The backend stops.
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "The YouTube sync is due: run {RunId} asked for.")]
    private partial void LogAsked(string runId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The YouTube sync's schedule can't be looked at right now.")]
    private partial void LogLookFailed(Exception error);
}
