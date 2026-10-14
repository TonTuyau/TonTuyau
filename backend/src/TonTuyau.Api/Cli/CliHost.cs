using TonTuyau.Api.Hosting;
using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;
using TonTuyau.Application.Validation;

namespace TonTuyau.Api.Cli;

/// <summary>
/// A host without web server or scheduler, for one CLI command: <c>sync status</c> reads Elasticsearch itself, the
/// others ask the backend through its Unix socket (<see cref="CliSocket"/>).
/// </summary>
internal static class CliHost
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (CliRunner.IsVersion(args))
        {
            await Console.Out.WriteLineAsync(CliRunner.VersionLine).ConfigureAwait(false);
            return CliRunner.Ok;
        }

        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddTonTuyauConfiguration();
        // Warnings and errors only, whatever appsettings.json says: in the image the working folder is /app, where
        // its "Default": "Information" would otherwise print every TubeArchivist request under the CLI's report.
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Logging.AddFilter(category: null, LogLevel.Warning);
        builder.Services.AddTonTuyau();
        using IHost host = builder.Build();
        try
        {
            host.Services.ValidateTonTuyauOptions();
        }
        catch (ValidationException ex)
        {
            await Console.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
            return CliRunner.Usage;
        }

        // The first Ctrl-C cancels the run (the backend stops it, the report comes); a second one leaves at once.
        using CancellationTokenSource cancel = new();
        int presses = 0;
        Console.CancelKeyPress += (_, e) =>
        {
            if (Interlocked.Increment(ref presses) == 1)
            {
                e.Cancel = true;
                cancel.Cancel();
            }
        };

        string? socket = CliSocket.PathFrom(builder.Configuration);
        using HttpClient http = socket is null ? new HttpClient() : CliSocket.Client(socket);
        CliRunner runner = new(host.Services, Console.Out, new BackendClient(http));
        if (!CliRunner.NeedsStore(args))
        {
            if (socket is null && CliRunner.IsCommand(args) && args.Length > 1)
            {
                await Console.Error.WriteLineAsync("The command line's socket is off (TONTUYAU_CLI_SOCKET is empty).")
                    .ConfigureAwait(false);
                return CliRunner.Usage;
            }

            // The backend does the work, or it's help or a usage error: never wait for Elasticsearch.
            return await runner.RunAsync(args, cancel.Token).ConfigureAwait(false);
        }

        try
        {
            // The backend may never have started: make sure the indices exist.
            await host.Services.GetRequiredService<IIndexManager>().EnsureAsync(cancel.Token).ConfigureAwait(false);
            await host.Services.GetRequiredService<ISyncStateStore>().EnsureAsync(cancel.Token).ConfigureAwait(false);
        }
        catch (SearchStoreException ex)
        {
            await Console.Error.WriteLineAsync("Elasticsearch: " + ex.Message).ConfigureAwait(false);
            return CliRunner.RunFailed;
        }

        return await runner.RunAsync(args, cancel.Token).ConfigureAwait(false);
    }
}
