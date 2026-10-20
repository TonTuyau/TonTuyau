using TonTuyau.Application.Library;
using TonTuyau.Application.Store;

namespace TonTuyau.Application.Sync;

public enum SyncMode
{
    /// <summary>
    /// The TubeArchivist import (2.0): its whole library walked, what TonTuyau doesn't have added; nothing deleted.
    /// </summary>
    Import,

    /// <summary>The index written again from PostgreSQL into a new generation (2.0: nothing read from TubeArchivist).</summary>
    IndexRebuild,

    /// <summary>Reads one video on YouTube (1.5): its state, and its metadata when it's there.</summary>
    RefreshVideo,

    /// <summary>Reads one channel on YouTube (1.5): its state, and its metadata when it's there.</summary>
    RefreshChannel,

    /// <summary>Reads one channel on YouTube, then each of its videos in the library (1.5).</summary>
    RefreshChannelVideos,

    /// <summary>
    /// Downloads one video from YouTube into the media folder (2.0), then adds it to the library, with its channel when
    /// the library doesn't have it.
    /// </summary>
    DownloadVideo,

    /// <summary>
    /// Adds a channel (2.0, B4): read on YouTube by its id or <c>@handle</c>, added to the library when it's new, and
    /// subscribed with <see cref="SyncRun.Tabs"/>. Nothing is downloaded: B5's syncs do.
    /// </summary>
    AddChannel,

    /// <summary>
    /// The incremental YouTube sync (2.0, B5): each subscribed channel's ticked tabs, newest first, down to the first
    /// video the library has; what's new is downloaded. A channel never synced is listed whole. Every few hours by
    /// itself (<see cref="YouTube.YouTubeSettings.SyncEveryHours"/>), or by hand.
    /// </summary>
    YouTubeSync,

    /// <summary>
    /// The full YouTube sync (2.0, B5): each subscribed channel's ticked tabs, whole; what the library doesn't have is
    /// downloaded, and so are the files missing of the videos it has. By hand only; of one channel alone too
    /// (<see cref="SyncRun.Target"/>: "Download everything" on its page).
    /// </summary>
    YouTubeFullSync,
}

public enum SyncTrigger
{
    /// <summary>By itself: a refresh resuming once its hold is over (1.5), the incremental YouTube sync (2.0).</summary>
    Scheduler,
    Cli,

    /// <summary>
    /// When the backend starts: the TubeArchivist import (2.0), or the index rebuild after changed index definitions
    /// (QA10 M5-10).
    /// </summary>
    Startup,

    /// <summary>From the admin's Tasks page (1.4), by the account <see cref="SyncRun.StartedBy"/>.</summary>
    Admin,
}

public enum SyncOutcome
{
    Running,
    Succeeded,
    Failed,

    /// <summary>Stopped because whoever started it asked (1.4): what it did stays, as after a failure.</summary>
    Canceled,

    /// <summary>
    /// A refresh YouTube refused (1.5: a bot check, too many requests): it waits, without the lock, and resumes by
    /// itself at <see cref="SyncRun.ResumeAt"/> (or when asked), from <see cref="SyncRun.Remaining"/>.
    /// </summary>
    OnHold,

    /// <summary>
    /// Asked for while another run went on (2.0, the run queue): it waits its turn, first come first served, and
    /// starts by itself (<see cref="SyncRun.StartedAt"/> is then when it starts).
    /// </summary>
    Queued,
}

/// <summary>Who holds the run lock, and until when (renewed while the run lives).</summary>
public sealed record SyncLockInfo(
    string Owner,
    SyncMode Mode,
    SyncTrigger Trigger,
    DateTimeOffset AcquiredAt,
    DateTimeOffset LeaseUntil);

/// <summary>Counters of one run. Everything a person needs to judge whether the run did what it should.</summary>
public sealed record SyncCounts
{
    public int VideosRead { get; init; }

    public int ChannelsRead { get; init; }

    public int Created { get; init; }

    /// <summary>
    /// The new videos by type (the dashboard's import history). Null in the runs of TonTuyau 1.1 and older, which
    /// only counted them all together.
    /// </summary>
    public TypeCounts? CreatedByType { get; init; }

    public int Updated { get; init; }

    public int Unchanged { get; init; }

    public int Rejected { get; init; }

    public int Corrected { get; init; }

    public int ArtCopied { get; init; }

    public int ArtFailed { get; init; }

    /// <summary>Resized versions (WebP, QA09) made by this run.</summary>
    public int ArtResized { get; init; }

    /// <summary>Resized versions this run couldn't make (logged; tried again at the next run).</summary>
    public int ArtResizeFailed { get; init; }

    public int MediaMissing { get; init; }

    /// <summary>A refresh (1.5): videos and channels YouTube answered with their metadata.</summary>
    public int FoundOnYouTube { get; init; }

    /// <summary>A refresh: of those found, the videos YouTube has unlisted (1.5).</summary>
    public int UnlistedOnYouTube { get; init; }

    /// <summary>A refresh: videos and channels YouTube says are removed, or their account terminated.</summary>
    public int GoneFromYouTube { get; init; }

    /// <summary>A refresh: videos YouTube says their owner made private (1.5).</summary>
    public int PrivateOnYouTube { get; init; }

    /// <summary>
    /// A YouTube sync (2.0): the videos it listed and didn't download (gone, private, members only…), noted each.
    /// </summary>
    public int DownloadFailed { get; init; }

    /// <summary>A refresh: checks without an answer to trust (a bot check, the network…), to try again.</summary>
    public int CheckFailed { get; init; }

    /// <summary>A refresh: the comments read with the videos (answers included).</summary>
    public int CommentsRead { get; init; }

    /// <summary>
    /// The channels subscribed to (2.0, B4): TubeArchivist's, by an import (with the channels it added), or the one
    /// "Add a channel" added.
    /// </summary>
    public int Subscribed { get; init; }
}

/// <summary>One run (an import, an index rebuild, a refresh, a download), as kept in the run history.</summary>
public sealed record SyncRun
{
    public required string Id { get; init; }

    public required SyncMode Mode { get; init; }

    public required SyncTrigger Trigger { get; init; }

    /// <summary>When it started; while it's queued, when it was asked for.</summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>When it was asked for (2.0: the queue); null for the runs TonTuyau starts at once by itself.</summary>
    public DateTimeOffset? QueuedAt { get; init; }

    /// <summary>The account that started it from the Tasks page (<see cref="SyncTrigger.Admin"/>), or null.</summary>
    public string? StartedBy { get; init; }

    public DateTimeOffset? FinishedAt { get; init; }

    public SyncOutcome Outcome { get; init; } = SyncOutcome.Running;

    public string? Error { get; init; }

    /// <summary>
    /// For a refresh (1.5): the video or channel it reads (its YouTube id); a download's video (2.0); the channel
    /// "Add a channel" adds (its id, or its <c>@handle</c> until the run reads its id on YouTube).
    /// </summary>
    public string? Target { get; init; }

    /// <summary>"Add a channel" (2.0, B4): the tabs it subscribes to; null for the other runs.</summary>
    public ChannelTabs? Tabs { get; init; }

    /// <summary>A refresh on hold (1.5): when it resumes by itself; null otherwise.</summary>
    public DateTimeOffset? ResumeAt { get; init; }

    /// <summary>A refresh on hold (1.5): the channels and videos left to read, in order (the one refused first).</summary>
    public IReadOnlyList<string> Remaining { get; init; } = [];

    public SyncCounts Counts { get; init; } = new();

    /// <summary>Things a person should know about: escalations, capped lists, channels not cleaned and why…</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];

    /// <summary>The first rejected documents (subject: errors), capped to keep the history small.</summary>
    public IReadOnlyList<string> RejectedSamples { get; init; } = [];

    /// <summary>The run rebuilt the indices (asked for, or before an import because the definitions changed).</summary>
    public bool IndexRebuild { get; init; }

    public static string NewId(DateTimeOffset now) =>
        $"run-{now.UtcDateTime:yyyyMMdd'T'HHmmss'Z'}-{Guid.NewGuid().ToString("N")[..6]}";
}
