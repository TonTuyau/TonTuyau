using TonTuyau.Application.Sync;

namespace TonTuyau.Infrastructure.Runs;

/// <summary>
/// A row of <c>runs</c> (2.0): one run of the history, whole in <see cref="Run"/> (JSON), with the columns its lists
/// are searched and sorted by.
/// </summary>
public sealed class RunRow
{
    public required string Id { get; set; }

    /// <summary>Stored as its name (<c>RefreshVideo</c>), as the other columns of this kind.</summary>
    public SyncMode Mode { get; set; }

    public SyncTrigger Trigger { get; set; }

    public SyncOutcome Outcome { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary>The run itself, as <see cref="RunJson"/> writes it.</summary>
    public required string Run { get; set; }
}
