using TonTuyau.Application.Sync;

namespace TonTuyau.Api.Cli;

/// <summary>
/// Shows a running sync's progress under the CLI: a line at each phase change, then at most one every
/// <see cref="Interval"/> (a 20-minute run prints about 130). On a terminal (<c>docker exec -it</c>) it's one line
/// rewritten in place; otherwise (<c>docker exec</c> without <c>-t</c>, piped into <c>tee</c>) each update is a line
/// of its own, so logs stay readable, and each phase's last state is shown before the next phase (its result stays in
/// the log). Thread-safe: the sync reports from several threads.
/// </summary>
/// <param name="inPlace">The output is a terminal: rewrite one line (<c>\r</c>, then clear the rest of it).</param>
/// <param name="width">The terminal's width (in-place only; 0 = unknown): a longer line is cut, since a line that
/// wraps can't be rewritten in place.</param>
internal sealed class CliProgress(TextWriter output, bool inPlace, TimeProvider clock, int width = 0)
    : ISyncProgressReporter
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    /// <summary>ANSI "erase to the end of the line": what's left of a longer previous line.</summary>
    private const string ClearToEndOfLine = "\u001b[K";

    private readonly Lock _gate = new();
    private SyncProgress? _latest;
    private SyncProgress? _shown;
    private DateTimeOffset _shownAt;
    private bool _lineOpen;

    public void Report(SyncProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        lock (_gate)
        {
            SyncProgress? previous = _latest;
            _latest = progress;
            DateTimeOffset now = clock.GetUtcNow();
            bool newPhase = _shown is null || progress.Phase != _shown.Phase;
            if (newPhase && !inPlace && previous is not null && !SameLine(previous, _shown))
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
    /// The run is over: shows its last state if that wasn't shown yet (so the final counts are on screen), and ends the
    /// in-place line so the report starts on a line of its own. Nothing when nothing was reported.
    /// </summary>
    public void Finish()
    {
        lock (_gate)
        {
            if (_latest is not null && !SameLine(_latest, _shown))
            {
                Show(_latest, clock.GetUtcNow());
            }

            if (_lineOpen)
            {
                output.WriteLine();
                _lineOpen = false;
            }
        }
    }

    // Under _gate.
    private void Show(SyncProgress progress, DateTimeOffset now)
    {
        string line = SyncProgressText.Line(progress, now - progress.StartedAt);
        if (inPlace)
        {
            output.Write("\r" + Fit(line) + ClearToEndOfLine);
            _lineOpen = true;
        }
        else
        {
            output.WriteLine(line);
        }

        output.Flush();
        _shown = progress;
        _shownAt = now;
    }

    // Whether showing it again would only repeat the line (a count the line doesn't show may have changed).
    private static bool SameLine(SyncProgress progress, SyncProgress? shown) =>
        shown is not null
        && SyncProgressText.Line(progress, TimeSpan.Zero) == SyncProgressText.Line(shown, TimeSpan.Zero);

    // One column short of the width: writing the last column makes some terminals wrap already.
    private string Fit(string line) => width > 1 && line.Length >= width ? line[..(width - 1)] : line;
}
