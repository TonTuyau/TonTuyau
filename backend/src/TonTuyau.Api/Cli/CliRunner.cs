using System.Globalization;
using TonTuyau.Api.Contracts;
using TonTuyau.Api.Hosting;
using TonTuyau.Application.Library;
using TonTuyau.Application.RunLogs;
using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;

namespace TonTuyau.Api.Cli;

/// <summary>
/// The <c>tontuyau</c> command line (inside the container: <c>docker exec tontuyau-backend tontuyau …</c>).
/// Exit codes: 0 done, 1 the run failed or was canceled (or the backend can't be reached), 2 usage error, 3 another
/// run is going on, 4 refused, 5 a refresh put on hold (YouTube refused: it resumes by itself after the cool-down).
/// <para>
/// Since 1.4 the commands that run something ask the backend to (<see cref="BackendClient"/>, through its Unix socket):
/// the run is in the backend's log and on the Tasks page. They print its log as it comes (its start, progress lines,
/// warnings, errors, end), then the report. Ctrl-C cancels the run; a second Ctrl-C leaves without waiting for it.
/// <c>status</c> reads PostgreSQL and Elasticsearch itself.
/// </para>
/// </summary>
internal sealed class CliRunner(IServiceProvider services, TextWriter output, BackendClient backend)
{
    public const int Ok = 0;
    public const int RunFailed = 1;
    public const int Usage = 2;
    public const int Busy = 3;
    public const int Refused = 4;
    public const int OnHold = 5;

    /// <summary>How often the run's log is asked for while it goes on.</summary>
    private static readonly TimeSpan PollEvery = TimeSpan.FromMilliseconds(500);

    public const string Help = """
        Usage: tontuyau <command>

          import                      import TubeArchivist's library now: what TonTuyau doesn't have is added
          status                      lock, pending art, indices, recent runs
          index rebuild               write the library into a new index generation, then switch to it
          youtube video <id>          read a video of the library on YouTube (its state, its metadata)
          youtube channel <id>        read a channel of the library on YouTube
          youtube channel-videos <id> read a channel, then each of its videos in the library, on YouTube
          health                      whether the backend running here answers (the container's health check)
          --version                   TonTuyau's version
        """;

    private static readonly string[] Verbs =
        ["import", "status", "index", "youtube", "health", "help", "--help", "-h", "--version", "version"];

    /// <summary>What <c>tontuyau --version</c> prints.</summary>
    public static string VersionLine => "TonTuyau " + AppVersion.Value;

    private SyncService Sync => services.GetRequiredService<SyncService>();

    public static bool IsCommand(string[] args) => args.Length > 0 && Verbs.Contains(args[0]);

    /// <summary><c>tontuyau --version</c>: answered without configuration or Elasticsearch.</summary>
    public static bool IsVersion(string[] args) => args is ["--version" or "version"];

    /// <summary><c>tontuyau health</c> (1.5.1): asks the backend here, without configuration (<see cref="HealthCheck"/>).</summary>
    public static bool IsHealth(string[] args) => args is ["health"];

    /// <summary>The command that reads the stores itself (the others go through the backend).</summary>
    public static bool NeedsStore(string[] args) => args is ["status"];

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        try
        {
            return args switch
            {
                ["import"] => ReportImport(await FollowAsync("cli/import", cancellationToken)),
                ["status"] => Status(await Sync.GetStatusAsync(cancellationToken)),
                ["index", "rebuild"] => Report(await FollowAsync("cli/index/rebuild", cancellationToken)),
                ["youtube", "video", string id] => LibraryIds.IsVideoId(id)
                    ? Report(await FollowAsync($"cli/youtube/video/{id}", cancellationToken))
                    : Write($"Refused: '{id}' isn't a video id (11 characters).", Refused),
                ["youtube", "channel" or "channel-videos", string id] => LibraryIds.IsChannelId(id)
                    ? Report(await FollowAsync($"cli/youtube/{args[1]}/{id}", cancellationToken))
                    : Write($"Refused: '{id}' isn't a channel id (UC and 22 characters).", Refused),
                ["help" or "--help" or "-h"] => Write(Help, Ok),
                ["--version" or "version"] => Write(VersionLine, Ok),
                _ => Write(Help, Usage),
            };
        }
        catch (CliRefusalException ex)
        {
            return Write(ex.Message, ex.ExitCode);
        }
        catch (HttpRequestException ex)
        {
            return Write(
                $"Can't reach the TonTuyau backend ({ex.Message}): the command line goes through it, is it running?",
                RunFailed);
        }
    }

    /// <summary>
    /// Starts the run in the backend and prints its log until it's over and all written, then gives the run back.
    /// Ctrl-C (<paramref name="cancellationToken"/>) asks the backend to cancel it, and the log goes on to its end.
    /// </summary>
    /// <param name="path">The command's endpoint in the backend.</param>
    private async Task<SyncRun> FollowAsync(string path, CancellationToken cancellationToken)
    {
        string id = await backend.StartAsync(path, CancellationToken.None);
        bool cancelAsked = false;
        long after = 0;
        while (true)
        {
            if (cancellationToken.IsCancellationRequested && !cancelAsked)
            {
                cancelAsked = true;
                Print("Ctrl-C: canceling the run (Ctrl-C again to leave without waiting).");
                await backend.CancelAsync(id, CancellationToken.None);
            }

            RunLogResponse log = await backend.GetLogAsync(id, after, CancellationToken.None);
            foreach (RunLogLineResponse line in log.Lines)
            {
                Print(line.Level switch
                {
                    RunLogLevel.Warning => "warning: " + line.Message,
                    RunLogLevel.Error => "error: " + line.Message,
                    _ => line.Message,
                });
                after = line.Id;
            }

            if (log.Complete)
            {
                return await backend.GetRunAsync(id, CancellationToken.None);
            }

            // Not with Ctrl-C's token: Ctrl-C is seen at the next turn, and the log still followed.
            await Task.Delay(PollEvery, CancellationToken.None);
        }
    }

    /// <summary>A line, at once (a pipe, e.g. to <c>tee</c>, gets it as it comes).</summary>
    private void Print(string line)
    {
        output.WriteLine(line);
        output.Flush();
    }

    private static string When(DateTimeOffset? at) =>
        at?.ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture) ?? "-";

    private int Write(string text, int code)
    {
        output.WriteLine(text);
        return code;
    }

    /// <summary>An import's report, saying first when it rebuilt the indices before (QA10 M5-10).</summary>
    private int ReportImport(SyncRun run)
    {
        if (run.IndexRebuild)
        {
            WriteLines("""
                Index rebuild pending (the index definitions changed):
                this run rebuilt the indices first, then imported.
                """);
        }

        return Report(run);
    }

    private int Report(SyncRun run)
    {
        WriteRun(run);
        return run.Outcome switch
        {
            SyncOutcome.Succeeded => Ok,
            SyncOutcome.OnHold => OnHold,
            _ => RunFailed,
        };
    }

    private int Status(SyncStatus status)
    {
        WriteLines(status.Lock is { } l
            ? $"""
                Lock           : held by {l.Owner} ({Name(l.Mode)}, {l.Trigger}) since {When(l.AcquiredAt)}
                                 lease until {When(l.LeaseUntil)}
                """
            : "Lock           : free");
        output.WriteLine($"Pending art    : {status.PendingArt}");
        if (status.Indices.Any(i => !i.DefinitionUpToDate))
        {
            WriteLines("""
                Index rebuild  : pending (the index definitions changed)
                                 automatic at startup, else at the next import,
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
        string mode = Name(run.Mode);
        string trigger = run.Trigger.ToString().ToLowerInvariant();
        string outcome = run.Outcome.ToString().ToUpperInvariant();
        string span = $"{When(run.StartedAt)} → {When(run.FinishedAt)}";
        output.WriteLine($"{indent}{run.Id}  {mode} by {trigger}  {outcome}  {span}");
        WriteLines(
            run.Mode is SyncMode.RefreshVideo or SyncMode.RefreshChannel or SyncMode.RefreshChannelVideos
                ? $"""
                      target    {run.Target}
                      read      {c.VideosRead} videos / {c.ChannelsRead} channels
                      youtube   {c.FoundOnYouTube} found ({c.UnlistedOnYouTube} unlisted), {c.GoneFromYouTube} gone,
                                {c.PrivateOnYouTube} private, {c.CheckFailed} to retry
                      comments  {c.CommentsRead} read
                      store     changed {c.Updated}, unchanged {c.Unchanged}
                    """
                : run.Mode == SyncMode.IndexRebuild
                ? """
                      index     written from the library into a new generation
                    """
                : $"""
                      read      {c.VideosRead} videos / {c.ChannelsRead} channels
                      store     new {c.Created}, already there {c.Unchanged}
                                rejected {c.Rejected}, corrected {c.Corrected}
                      art       {c.ArtCopied} copied / {c.ArtFailed} failed
                      resized   {c.ArtResized} versions made / {c.ArtResizeFailed} failed
                      files     {c.MediaMissing} missing
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

    /// <summary>As the commands spell it: import, index rebuild, youtube video…</summary>
    private static string Name(SyncMode mode) => mode switch
    {
        SyncMode.IndexRebuild => "index rebuild",
        SyncMode.RefreshVideo => "youtube video",
        SyncMode.RefreshChannel => "youtube channel",
        SyncMode.RefreshChannelVideos => "youtube channel-videos",
        _ => mode.ToString().ToLowerInvariant(),
    };
}
