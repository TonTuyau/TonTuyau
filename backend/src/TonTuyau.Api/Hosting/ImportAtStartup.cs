using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;

namespace TonTuyau.Api.Hosting;

/// <summary>
/// Imports TubeArchivist's library when the backend starts (2.0), when its settings are there: what TonTuyau doesn't
/// have is added, nothing else changes. Once it ran, TubeArchivist can be shut down and its settings removed. It waits
/// for the indices first; another run holding the lock (the rebuild at startup, a refresh) makes it try again later.
/// </summary>
internal sealed partial class ImportAtStartup(
    SyncService sync,
    SyncRunner runner,
    TimeProvider clock,
    ILogger<ImportAtStartup> logger) : BackgroundService
{
    /// <summary>How long to wait between two tries, while the indices aren't there or another run goes on.</summary>
    public static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!sync.CanImport)
        {
            LogNoTubeArchivist();
            return;
        }

        // Never holds up startup: what follows goes to the network.
        await Task.Yield();
        try
        {
            while (!await TryImportAsync(stoppingToken).ConfigureAwait(false))
            {
                await Task.Delay(RetryDelay, clock, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The backend stops.
        }
    }

    /// <returns>False when it's to try again: the indices aren't there yet, or another run goes on.</returns>
    private async Task<bool> TryImportAsync(CancellationToken stoppingToken)
    {
        try
        {
            // The indices exist (the backend makes them as it starts): the import writes to them.
            await sync.IsRebuildPendingAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (SearchStoreException ex)
        {
            LogRetry(ex.Message, RetryDelay.TotalSeconds);
            return false;
        }

        try
        {
            LogStarting();
            SyncRun run = await runner.RunImportAsync(SyncTrigger.Startup, stoppingToken).ConfigureAwait(false);
            LogFinished(run.Id, run.Outcome);
            return true;
        }
        catch (SyncBusyException ex)
        {
            LogRetry(ex.Message, RetryDelay.TotalSeconds);
            return false;
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "No TubeArchivist import at startup: its settings (TONTUYAU_TA_URL, TONTUYAU_TA_TOKEN) aren't there.")]
    private partial void LogNoTubeArchivist();

    [LoggerMessage(Level = LogLevel.Information, Message = "Importing TubeArchivist's library, in the background.")]
    private partial void LogStarting();

    [LoggerMessage(Level = LogLevel.Information, Message = "TubeArchivist import {RunId}: {Outcome}.")]
    private partial void LogFinished(string runId, SyncOutcome outcome);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "TubeArchivist import not started now ({Reason}); trying again in {Seconds} s.")]
    private partial void LogRetry(string reason, double seconds);
}
