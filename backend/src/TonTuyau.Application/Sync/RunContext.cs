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
/// <param name="progressStart">When the command started, for the elapsed time of the progress.</param>
internal sealed class RunContext(
    ISyncStateStore state,
    SyncRun run,
    IReadOnlySet<string> pendingArt,
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

    /// <summary>The videos whose file the walk didn't find on the media share (<see cref="CheckedFile"/>).</summary>
    public HashSet<string> MissingMedia { get; } = new(StringComparer.Ordinal);

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

    /// <summary>Counts rejected documents (not imported), and keeps the first ones as samples.</summary>
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
    }

    public async Task SaveAsync()
    {
        lock (_gate)
        {
            Run = Run with { Notes = [.. _notes], RejectedSamples = [.. _rejectedSamples] };
        }

        await state.SaveRunAsync(Run, CancellationToken.None).ConfigureAwait(false);
    }

    // Under _gate. Nothing before the first phase.
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
