using System.Data.Common;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TonTuyau.Application.RunLogs;

namespace TonTuyau.Infrastructure.RunLogs;

/// <summary>
/// Writes the runs' log lines to PostgreSQL in the background, the lines of a second at once: logging never waits for
/// the database. Lines that can't be written wait and are tried again; past <see cref="MaxWaiting"/> (a database down
/// for a long time), the oldest are dropped. The backend stopping writes what's left.
/// </summary>
internal sealed partial class RunLogWriter(IServiceScopeFactory scopes, IServiceProvider services) : BackgroundService
{
    public const int MaxWaiting = 50_000;

    private const int BatchSize = 500;
    private static readonly TimeSpan Every = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(5);

    private readonly Channel<RunLogLine> _lines = Channel.CreateBounded<RunLogLine>(
        new BoundedChannelOptions(MaxWaiting) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    /// <summary>A line to write: never waits, never fails.</summary>
    public void Add(RunLogLine line) => _lines.Writer.TryWrite(line);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Not from the constructor: the logger factory needs the run log's provider, which needs this writer.
        ILogger logger = services.GetRequiredService<ILogger<RunLogWriter>>();
        List<RunLogLine> batch = [];
        try
        {
            while (await _lines.Reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
            {
                // The lines of a second at once: a run writing fast makes a batch a second, not a row a line.
                await Task.Delay(Every, stoppingToken).ConfigureAwait(false);
                if (!await WriteAsync(batch, logger, stoppingToken).ConfigureAwait(false))
                {
                    await Task.Delay(RetryAfter, stoppingToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The backend stops: what's left is written below.
        }

        await WriteAsync(batch, logger, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>Writes what waits, the batch that failed last time first.</summary>
    /// <returns>False when the database refused a batch (it stays in <paramref name="batch"/>).</returns>
    private async Task<bool> WriteAsync(List<RunLogLine> batch, ILogger logger, CancellationToken cancellationToken)
    {
        while (true)
        {
            while (batch.Count < BatchSize && _lines.Reader.TryRead(out RunLogLine? line))
            {
                batch.Add(line);
            }

            if (batch.Count == 0)
            {
                return true;
            }

            try
            {
                AsyncServiceScope scope = scopes.CreateAsyncScope();
                await using (scope.ConfigureAwait(false))
                {
                    await scope.ServiceProvider
                        .GetRequiredService<IRunLogRepository>()
                        .AddAsync(batch, cancellationToken)
                        .ConfigureAwait(false);
                }

                batch.Clear();
            }
            catch (Exception ex) when (ex is DbException or DbUpdateException)
            {
                LogNotWritten(logger, batch.Count, ex.Message);
                return false;
            }
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Couldn't write {Count} lines of the runs' logs to the database ({Reason}): trying again.")]
    private static partial void LogNotWritten(ILogger logger, int count, string reason);
}
