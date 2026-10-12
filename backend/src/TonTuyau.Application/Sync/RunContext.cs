using TonTuyau.Application.TubeArchivist;

namespace TonTuyau.Application.Sync;

/// <summary>
/// The state of one run while it executes: counters, notes, rejected samples, pending art, progress saves, and the
/// progress shown to whoever watches (each count change and phase step is reported).
/// </summary>
/// <remarks>
/// <see cref="Count"/> may be called from several threads at once (the art resizing runs in parallel): the counts and
/// the progress change under one lock, and each report is made under it, so reports arrive in order.
/// </remarks>
/// <param name="guardPercent">Stop when more than this share of the documents read is rejected (100 = never).</param>
/// <param name="guardMinimum">Don't judge before this many documents are read.</param>
/// <param name="progressStart">When the command started, for the elapsed time of the progress.</param>
internal sealed class RunContext(
    ISyncStateStore state,
    SyncRun run,
    IReadOnlySet<string> pendingArt,
    int guardPercent,
    int guardMinimum,
    ISyncProgressReporter progress,
    DateTimeOffset progressStart,
    CancellationToken cancellationToken)
{
    public const int MaxRejectedSamples = 50;

    private readonly List<string> _notes = [.. run.Notes];
    private readonly List<string> _rejectedSamples = [.. run.RejectedSamples];
    private readonly Lock _gate = new();
    private SyncPhase? _phase;
    private int _done;
    private int? _total;
    private int _filesChecked;

    public SyncRun Run { get; private set; } = run;

    public CancellationToken CancellationToken { get; } = cancellationToken;

    public HashSet<string> PendingArt { get; } = new(pendingArt, StringComparer.Ordinal);

    public HashSet<string> KnownChannels { get; } = new(StringComparer.Ordinal);

    /// <summary>The videos whose file the walk didn't find on the media share (<see cref="CheckedFile"/>).</summary>
    public HashSet<string> MissingMedia { get; } = new(StringComparer.Ordinal);

    /// <summary>Highest TubeArchivist <c>date_downloaded</c> seen in this run (epoch seconds).</summary>
    public long? MaxDownloaded { get; private set; }

    /// <summary>Changes the counts (thread-safe) and reports the progress.</summary>
    public void Count(Func<SyncCounts, SyncCounts> change)
    {
        lock (_gate)
        {
            Run = Run with { Counts = change(Run.Counts) };
            Report();
        }
    }

    public void Note(string note) => _notes.Add(note);

    public void Update(Func<SyncRun, SyncRun> change)
    {
        lock (_gate)
        {
            Run = change(Run);
        }
    }

    /// <summary>A new step: reported at once.</summary>
    /// <param name="total">The step's end, only when exactly known (see <see cref="SyncProgress.Total"/>).</param>
    public void BeginPhase(SyncPhase phase, int? total = null)
    {
        lock (_gate)
        {
            _phase = phase;
            _done = 0;
            _total = total;
            Report();
        }
    }

    /// <summary>How much of the current step is done (see <see cref="SyncProgress.Done"/>).</summary>
    public void Progressed(int done)
    {
        lock (_gate)
        {
            _done = done;
            Report();
        }
    }

    /// <summary>A video file looked for on the media share; a missing one is remembered.</summary>
    public void CheckedFile(string videoId, bool exists)
    {
        lock (_gate)
        {
            _filesChecked++;
            if (!exists)
            {
                MissingMedia.Add(videoId);
            }
        }
    }

    public void SawDownloaded(DateTimeOffset downloaded)
    {
        long seconds = downloaded.ToUnixTimeSeconds();
        MaxDownloaded = MaxDownloaded is not null ? Math.Max(MaxDownloaded.Value, seconds) : seconds;
    }

    /// <summary>Counts rejected documents and stops the run when most of what TubeArchivist sends is broken.</summary>
    public void Rejected(IReadOnlyList<RejectedItem> rejected)
    {
        if (rejected.Count > 0)
        {
            Count(c => c with { Rejected = c.Rejected + rejected.Count });
            foreach (RejectedItem? item in rejected.Take(MaxRejectedSamples - _rejectedSamples.Count))
            {
                _rejectedSamples.Add(
                    $"{item.Subject}: " + string.Join("; ", item.Errors.Select(e => $"{e.Field} {e.Message}")));
            }
        }

        int read = Run.Counts.VideosRead + Run.Counts.ChannelsRead;
        // Both conditions: the minimum is a gate (too few documents to judge), the share decides once past it.
        if (read >= guardMinimum && Run.Counts.Rejected * 100L > read * (long)guardPercent)
        {
            throw new TubeArchivistFormatChangedException(read, Run.Counts.Rejected);
        }
    }

    public async Task SaveAsync()
    {
        lock (_gate)
        {
            Run = Run with { Notes = [.. _notes], RejectedSamples = [.. _rejectedSamples] };
        }

        await state.SaveRunAsync(Run, CancellationToken.None).ConfigureAwait(false);
    }

    // Under _gate. Nothing before the first phase (e.g. apply-deletions starting from the latest run's counts).
    private void Report()
    {
        if (_phase is not null)
        {
            progress.Report(new SyncProgress(
                _phase.Value,
                progressStart,
                _done,
                _total,
                Run.Counts,
                _filesChecked,
                MissingMedia.Count));
        }
    }
}
