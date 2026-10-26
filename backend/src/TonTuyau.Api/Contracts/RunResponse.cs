using TonTuyau.Application.Sync;
using TonTuyau.Application.Tasks;

namespace TonTuyau.Api.Contracts;

/// <summary>A run of the history (contract v1.7, TonTuyau 1.4).</summary>
/// <param name="Task">
/// import | indexRebuild (contract v2.0) | refreshVideo | refreshChannel | refreshChannelVideos | downloadVideo |
/// addChannel (v2.3) | youTubeSync | youTubeFullSync | retryDownloads (v2.4).
/// </param>
/// <param name="Trigger">
/// What started it: scheduler (a refresh resuming by itself; v2.4: the incremental YouTube sync) | cli | startup (the
/// import, or the rebuild after an upgrade) | admin.
/// </param>
/// <param name="StartedBy">For <c>admin</c>: the account that started it; null otherwise.</param>
/// <param name="State">
/// queued (2.0: waiting its turn in the run queue; v2.14: a run held, back in it to resume) | running | succeeded |
/// failed | canceled | onHold (1.5: a refresh YouTube refused, waiting; v2.14: <see cref="HeldFor"/> says why).
/// </param>
/// <param name="StartedAt">When it started; while it's queued, when it was asked for.</param>
/// <param name="QueuedAt">When it was asked for (2.0); null for a run TonTuyau started at once by itself.</param>
/// <param name="Position">Queued (2.0): its place in line, 1 for the next to start; null otherwise.</param>
/// <param name="Error">Why it failed; null otherwise.</param>
/// <param name="Target">
/// A refresh (1.5): the video's or channel's YouTube id it reads; a download's video (2.0); the channel
/// <c>addChannel</c> adds (v2.3: its id, or its <c>@handle</c> until the run read its id).
/// </param>
/// <param name="Tabs"><c>addChannel</c> (v2.3): the tabs it subscribes to; null for the other runs.</param>
/// <param name="ResumeAt">A refresh on hold (1.5, <c>onHold</c>): when it resumes by itself; null otherwise, and when put on hold by hand (it waits for Resume now).</param>
/// <param name="HeldFor">
/// On hold (contract v2.14): why, youTube (YouTube refused: the queue waits for it) | hand (put on hold by hand: the
/// queue goes on) | diskSpace (too little room for its downloads: the queue goes on); null otherwise, and for a run held
/// before 2.3.0 (YouTube's).
/// </param>
/// <param name="FirstAt">
/// Queued (v2.14): when it was asked to go first (Run now, Resume now while another run went on, a run held to let
/// another go); the latest asked goes first, before the runs never asked. Null otherwise.
/// </param>
/// <param name="Left">A refresh on hold: how many channels and videos it has left to read; 0 otherwise.</param>
/// <param name="IndexRebuild">It rebuilt the indices (asked for, or before an import after an upgrade).</param>
/// <param name="Notes">What a person should know: a rebuild first, capped lists, files missing…</param>
/// <param name="Cancellable">
/// It's going on in this backend, on hold, or queued: <c>POST …/cancel</c> stops it (1.4), or takes it out of the queue.
/// </param>
public sealed record RunResponse(
    string Id,
    SyncMode Task,
    SyncTrigger Trigger,
    string? StartedBy,
    SyncOutcome State,
    DateTime StartedAt,
    DateTime? QueuedAt,
    int? Position,
    DateTime? FinishedAt,
    string? Error,
    RunCounts Counts,
    string? Target,
    ChannelTabsResponse? Tabs,
    DateTime? ResumeAt,
    HoldReason? HeldFor,
    DateTime? FirstAt,
    int Left,
    bool IndexRebuild,
    IReadOnlyList<string> Notes,
    bool Cancellable)
{
    public static explicit operator RunResponse(RunView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        SyncRun run = view.Run;
        return new RunResponse(
            run.Id,
            run.Mode,
            run.Trigger,
            run.StartedBy,
            run.Outcome,
            run.StartedAt.UtcDateTime,
            run.QueuedAt?.UtcDateTime,
            view.Position,
            run.FinishedAt?.UtcDateTime,
            run.Error,
            (RunCounts)run.Counts,
            run.Target,
            run.Tabs is null ? null : (ChannelTabsResponse)run.Tabs,
            run.ResumeAt?.UtcDateTime,
            run.HeldFor,
            run.FirstAt?.UtcDateTime,
            run.Remaining.Count,
            run.IndexRebuild,
            run.Notes,
            view.Cancellable);
    }
}
