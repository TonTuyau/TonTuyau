using TonTuyau.Application.Tasks;

namespace TonTuyau.Api.Contracts;

/// <summary>A run's log lines, oldest first (contract v1.7).</summary>
/// <param name="Complete">
/// No line will come after these: the run is over (a few seconds ago at least). While false, ask again for the lines
/// after the last one. A run from before TonTuyau 1.4 has no line, and is complete.
/// </param>
public sealed record RunLogResponse(IReadOnlyList<RunLogLineResponse> Lines, bool Complete)
{
    public static explicit operator RunLogResponse(RunLogView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new RunLogResponse([.. view.Lines.Select(l => (RunLogLineResponse)l)], view.Complete);
    }
}
