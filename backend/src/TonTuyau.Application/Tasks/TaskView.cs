using TonTuyau.Application.Sync;

namespace TonTuyau.Application.Tasks;

/// <summary>A task of the Tasks page: what it is, how it last ran, and why it can't start when it can't.</summary>
/// <param name="Unavailable">
/// Why it can't start (2.0: the import without TubeArchivist's settings); null when it can.
/// </param>
/// <param name="Next">
/// When it runs by itself next (2.0, the incremental YouTube sync: in the past when it's due now); null: by hand only.
/// </param>
public sealed record TaskView(SyncMode Task, RunView? LastRun, string? Unavailable, DateTimeOffset? Next = null);
