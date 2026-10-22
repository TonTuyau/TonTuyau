namespace TonTuyau.Application.Sync;

/// <summary>
/// A run's progress as plain lines, the same in a terminal and in a log (QA16-1: like the <c>tee</c> log):
/// the CLI prints them, and every run writes them to its log (1.4). A line at each phase change, then at most one
/// every <see cref="Interval"/> (a 20-minute run prints about 130), and each phase's last state before the next phase
/// (its result stays on screen). Thread-safe: the import reports from several threads.
/// <para>
/// A heartbeat (QA16-3 b): when nothing was shown for <see cref="Interval"/> (a long step with nothing new to report:
/// a slow TubeArchivist answer, the index switch), the current line is shown again, so the time keeps going up and the
/// run is visibly alive. It never adds a line closer than <see cref="Interval"/> to the previous one: when updates
/// come every few seconds, the lines are the same as without it. <see cref="Finish"/> stops it.
/// </para>
/// </summary>
/// <param name="write">Called with each line, under a lock: one at a time, in order.</param>
public sealed class ProgressLines(Action<string> write, TimeProvider clock) : ISyncProgressReporter, IDisposable
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    private readonly Lock _gate = new();
    private SyncProgress? _latest;
    private SyncProgress? _shown;
    private DateTimeOffset _shownAt;

    /// <summary>Armed at each line shown, for <see cref="Interval"/> later; null until the first line.</summary>
    private ITimer? _heartbeat;
    private bool _finished;

    public void Report(SyncProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        lock (_gate)
        {
            if (_finished)
            {
                return;
            }

            SyncProgress? previous = _latest;
            _latest = progress;
            DateTimeOffset now = clock.GetUtcNow();
            bool newPhase = _shown is null || progress.Phase != _shown.Phase;
            if (newPhase && previous is not null && !SameLine(previous, _shown))
            {
                Show(previous, now);
            }

            if (newPhase || now - _shownAt >= Interval)
            {
                Show(progress, now);
            }
        }
    }

    /// <summary>
    /// The run is over: shows its last state if that wasn't shown yet (so the final counts are on screen) and stops
    /// the heartbeat, so the report comes right after. Nothing is written after it, and calling it again does nothing.
    /// </summary>
    public void Finish()
    {
        lock (_gate)
        {
            if (_finished)
            {
                return;
            }

            _finished = true;
            _heartbeat?.Dispose();
            if (_latest is not null && !SameLine(_latest, _shown))
            {
                Show(_latest, clock.GetUtcNow());
            }
        }
    }

    /// <summary>Stops the heartbeat without writing anything (<see cref="Finish"/> is what shows the last state).</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _finished = true;
            _heartbeat?.Dispose();
        }
    }

    // The heartbeat's timer: the current line again, with the time since the start, if nothing was shown since.
    private void Beat()
    {
        lock (_gate)
        {
            if (_finished || _latest is null)
            {
                return;
            }

            DateTimeOffset now = clock.GetUtcNow();
            TimeSpan quiet = now - _shownAt;
            if (quiet >= Interval)
            {
                Show(_latest, now);
            }
            else
            {
                // A line was shown while this call waited for the lock: wait for the rest of the interval.
                _heartbeat?.Change(Interval - quiet, Timeout.InfiniteTimeSpan);
            }
        }
    }

    // Under _gate.
    private void Show(SyncProgress progress, DateTimeOffset now)
    {
        write(SyncProgressText.Line(progress, now - progress.StartedAt));
        _shown = progress;
        _shownAt = now;
        if (!_finished)
        {
            _heartbeat ??= clock.CreateTimer(_ => Beat(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _heartbeat.Change(Interval, Timeout.InfiniteTimeSpan);
        }
    }

    // Whether showing it again would only repeat the line (a count the line doesn't show may have changed).
    private static bool SameLine(SyncProgress progress, SyncProgress? shown) =>
        shown is not null
        && SyncProgressText.Line(progress, TimeSpan.Zero) == SyncProgressText.Line(shown, TimeSpan.Zero);
}
