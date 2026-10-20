using TonTuyau.Application.Sync;

namespace TonTuyau.Api.Hosting;

/// <summary>
/// Looks at the run queue every few seconds (2.0): a run of this backend ending starts the next one at once, but the
/// lock another process held, the startup index rebuild ending, or a run on hold canceled elsewhere only show here.
/// </summary>
internal sealed class RunQueueWatcher(SyncRunner runner, TimeProvider clock) : BackgroundService
{
    public static readonly TimeSpan Every = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(Every, clock, stoppingToken).ConfigureAwait(false);
                await runner.TryStartNextAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The backend stops.
        }
    }
}
