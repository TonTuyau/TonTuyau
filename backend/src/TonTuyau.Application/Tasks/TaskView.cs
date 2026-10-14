using TonTuyau.Application.Sync;

namespace TonTuyau.Application.Tasks;

/// <summary>A task of the Tasks page: what it is, when it runs next, how it last ran.</summary>
/// <param name="Schedule">Its cron, in the sync's time zone; null for applying deletions (never scheduled).</param>
/// <param name="Next">When the scheduler runs it next; null when it won't (never scheduled, or scheduler off).</param>
/// <param name="Waiting">For applying deletions: the full run whose list waits to be applied, or null.</param>
public sealed record TaskView(
    SyncMode Task,
    string? Schedule,
    DateTimeOffset? Next,
    RunView? LastRun,
    SyncRun? Waiting);
