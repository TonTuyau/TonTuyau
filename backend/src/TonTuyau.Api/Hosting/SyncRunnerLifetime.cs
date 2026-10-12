using TonTuyau.Application.Sync;

namespace TonTuyau.Api.Hosting;

/// <summary>
/// When the backend stops, cancels the run going on (<see cref="SyncRunner.StopAsync"/>) and waits for it to record
/// its end: registered after the runs' log writer, so it stops before it and the run's last lines are written.
/// </summary>
internal sealed class SyncRunnerLifetime(SyncRunner runner) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => runner
        .StopAsync()
        .WaitAsync(cancellationToken);
}
