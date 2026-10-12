using System.Globalization;
using TonTuyau.Api.Hosting;
using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;

namespace TonTuyau.Api.Cli;

/// <summary>
/// The <c>tontuyau</c> command line (inside the container: <c>docker exec tontuyau-backend tontuyau …</c>).
/// Exit codes: 0 done, 1 the run failed, 2 usage error, 3 another sync is running, 4 refused.
/// The commands that run a sync print its progress while it runs (see <see cref="CliProgress"/>), then the report.
/// </summary>
internal sealed class CliRunner(IServiceProvider services, TextWriter output)
{
    public const int Ok = 0;
    public const int RunFailed = 1;
    public const int Usage = 2;
    public const int Busy = 3;
    public const int Refused = 4;

    public const string Help = """
        Usage: tontuyau <command>

          sync incremental            read TubeArchivist's newest downloads now
          sync full                   walk the whole library now (refresh + checked deletions)
          sync status                 watermark, lock, pending art, indices, recent runs
          sync apply-deletions <run>  delete a blocked full run's list (each item confirmed with TubeArchivist)
          index rebuild               re-import everything into a new index generation, then switch to it
          --version                   TonTuyau's version
        """;

    private static readonly string[] Verbs = ["sync", "index", "help", "--help", "-h", "--version", "version"];

    /// <summary>What <c>tontuyau --version</c> prints.</summary>
    public static string VersionLine => "TonTuyau " + AppVersion.Value;

    private SyncService Sync => services.GetRequiredService<SyncService>();

    public static bool IsCommand(string[] args) => args.Length > 0 && Verbs.Contains(args[0]);

    /// <summary><c>tontuyau --version</c>: answered without configuration or Elasticsearch.</summary>
    public static bool IsVersion(string[] args) => args is ["--version" or "version"];

    /// <summary>A command that does real work (needs Elasticsearch), as opposed to help or a usage error.</summary>
    public static bool NeedsStore(string[] args) => args switch
    {
        ["sync", "incremental" or "full" or "status"] => true,
        ["sync", "apply-deletions", _] => true,
        ["index", "rebuild"] => true,
        _ => false,
    };

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        try
        {
            return args switch
            {
                ["sync", "incremental"] => ReportSync(await WithProgressAsync(
                    progress => Sync.RunAsync(SyncMode.Incremental, SyncTrigger.Cli, progress, cancellationToken))),
                ["sync", "full"] => ReportSync(await WithProgressAsync(
                    progress => Sync.RunAsync(SyncMode.Full, SyncTrigger.Cli, progress, cancellationToken))),
                ["sync", "status"] => Status(await Sync.GetStatusAsync(cancellationToken)),
                ["sync", "apply-deletions", string run] => Report(await WithProgressAsync(
                    progress => Sync.ApplyDeletionsAsync(run, progress, cancellationToken))),
                ["index", "rebuild"] => Report(await WithProgressAsync(
                    progress => Sync.RebuildAsync(SyncTrigger.Cli, progress, cancellationToken))),
                ["help" or "--help" or "-h"] => Write(Help, Ok),
                ["--version" or "version"] => Write(VersionLine, Ok),
                _ => Write(Help, Usage),
            };
        }
        catch (SyncBusyException ex)
        {
            return Write(ex.Message, Busy);
        }
        catch (SyncRefusedException ex)
        {
            return Write("Refused: " + ex.Message, Refused);
        }
    }

    /// <summary>Runs a sync with its progress shown, and ends the progress (even on an error) before the report.</summary>
    private async Task<SyncRun> WithProgressAsync(Func<ISyncProgressReporter, Task<SyncRun>> sync)
    {
        using CliProgress progress = new(output, services.GetService<TimeProvider>() ?? TimeProvider.System);
        try
        {
            return await sync(progress);
        }
        finally
        {
            progress.Finish();
        }
    }

    private static string When(DateTimeOffset? at) =>
        at?.ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture) ?? "-";

    private int Write(string text, int code)
    {
        output.WriteLine(text);
        return code;
    }

    /// <summary>A sync's report, saying first when it rebuilt the indices instead (QA10 M5-10).</summary>
    private int ReportSync(SyncRun run)
    {
        if (run.IndexRebuild)
        {
            WriteLines("""
                Index rebuild pending (the index definitions changed):
                this run rebuilt the indices instead of the sync asked for.
                """);
        }

        return Report(run);
    }

    private int Report(SyncRun run)
    {
        WriteRun(run);
        return run.Outcome == SyncOutcome.Succeeded ? Ok : RunFailed;
    }

    private int Status(SyncStatus status)
    {
        string watermark = status.Checkpoint.Watermark is not null
            ? When(DateTimeOffset.FromUnixTimeSeconds(status.Checkpoint.Watermark.Value))
            : "none (next run is a full one)";
        output.WriteLine(
            $"Watermark      : {watermark}{(status.Checkpoint.FullRunRequested ? " — full run requested" : "")}");
        WriteLines(status.Lock is { } l
            ? $"""
                Lock           : held by {l.Owner} ({l.Mode}, {l.Trigger}) since {When(l.AcquiredAt)}
                                 lease until {When(l.LeaseUntil)}
                """
            : "Lock           : free");
        output.WriteLine($"Pending art    : {status.PendingArt}");
        if (status.Indices.Any(i => !i.DefinitionUpToDate))
        {
            WriteLines("""
                Index rebuild  : pending (the index definitions changed)
                                 automatic at startup when the scheduler is on, else at the next sync,
                                 or now with `tontuyau index rebuild`
                """);
        }

        foreach (IndexStatus index in status.Indices)
        {
            string generations = $"reads {index.ReadIndex}, writes {index.WriteIndex}";
            string outdated = index.DefinitionUpToDate ? "" : " — OUTDATED definition (rebuild pending)";
            output.WriteLine($"Index {Name(index.Index),-9}: {generations}, {index.Documents:N0} documents{outdated}");
        }

        output.WriteLine("Recent runs    :");
        foreach (SyncRun run in status.RecentRuns)
        {
            WriteRun(run, indent: "  ");
        }

        return Ok;
    }

    private void WriteRun(SyncRun run, string indent = "")
    {
        SyncCounts c = run.Counts;
        string mode = run.Mode.ToString().ToLowerInvariant();
        string trigger = run.Trigger.ToString().ToLowerInvariant();
        string outcome = run.Outcome.ToString().ToUpperInvariant();
        WriteLines(
            $"""
            {run.Id}  {mode} by {trigger}  {outcome}  {When(run.StartedAt)} → {When(run.FinishedAt)}
              read      {c.VideosRead} videos / {c.ChannelsRead} channels
              store     new {c.Created}, changed {c.Updated}, unchanged {c.Unchanged}
                        rejected {c.Rejected}, corrected {c.Corrected}
              art       {c.ArtCopied} copied / {c.ArtFailed} failed
              resized   {c.ArtResized} versions made / {c.ArtResizeFailed} failed
              files     {c.MediaMissing} missing
              deletions {c.Deleted} deleted, {c.KeptStillInTubeArchivist} kept (still in TubeArchivist)
            """,
            indent);
        if (run.Error is not null)
        {
            output.WriteLine($"{indent}  error: {run.Error}");
        }

        // A note can span several lines (e.g. the command to run on a line of its own): the first gets the bullet,
        // the others line up under its text.
        foreach (string note in run.Notes)
        {
            string[] lines = note.ReplaceLineEndings("\n").Split('\n');
            output.WriteLine($"{indent}  - {lines[0]}");
            WriteLines(string.Join('\n', lines[1..]), $"{indent}    ");
        }
    }

    /// <summary>Writes a block of lines (a raw string), each prefixed with <paramref name="indent"/>.</summary>
    private void WriteLines(string block, string indent = "")
    {
        foreach (string line in block.ReplaceLineEndings("\n").Split('\n'))
        {
            output.WriteLine(indent + line);
        }
    }

    private static string Name(StoreIndex index) => index.ToString().ToLowerInvariant();
}
