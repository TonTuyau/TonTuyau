using TonTuyau.Application.Sync;

namespace TonTuyau.Api.Contracts;

/// <summary>The deletion list waiting to be applied: the latest full run's, over the limit (contract v1.7).</summary>
/// <param name="RunId">That full run.</param>
/// <param name="Count">How many videos and channels it lists.</param>
public sealed record WaitingDeletions(string RunId, DateTime StartedAt, int Count)
{
    public static explicit operator WaitingDeletions(SyncRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return new WaitingDeletions(run.Id, run.StartedAt.UtcDateTime, run.DeletionsPlanned);
    }
}
