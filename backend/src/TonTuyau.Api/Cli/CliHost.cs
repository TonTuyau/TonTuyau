using TonTuyau.Api.Hosting;
using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;
using TonTuyau.Application.Validation;

namespace TonTuyau.Api.Cli;

/// <summary>A host without web server or scheduler, for one CLI command; shares the backend's services and lock.</summary>
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

        using CancellationTokenSource cancel = new();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancel.Cancel();
        };

        if (!CliRunner.NeedsStore(args))
        {
            // Help or a usage error: never wait for Elasticsearch to say so.
            return await new CliRunner(host.Services, Console.Out).RunAsync(args, cancel.Token).ConfigureAwait(false);
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

        // The progress is plain lines, terminal or not (QA16-1).
        CliRunner runner = new(host.Services, Console.Out);
        return await runner.RunAsync(args, cancel.Token).ConfigureAwait(false);
    }
}
