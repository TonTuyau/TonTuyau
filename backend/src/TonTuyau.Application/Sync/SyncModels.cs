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
}

public enum SyncTrigger
{
    /// <summary>By itself: a refresh resuming once its hold is over (1.5).</summary>
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

    /// <summary>A refresh: checks without an answer to trust (a bot check, the network…), to try again.</summary>
    public int CheckFailed { get; init; }

    /// <summary>A refresh: the comments read with the videos (answers included).</summary>
    public int CommentsRead { get; init; }
}

/// <summary>One run (an import, an index rebuild, a refresh), as kept in the run history.</summary>
public sealed record SyncRun
{
    public required string Id { get; init; }

    public required SyncMode Mode { get; init; }

    public required SyncTrigger Trigger { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>The account that started it from the Tasks page (<see cref="SyncTrigger.Admin"/>), or null.</summary>
    public string? StartedBy { get; init; }

    public DateTimeOffset? FinishedAt { get; init; }

    public SyncOutcome Outcome { get; init; } = SyncOutcome.Running;

    public string? Error { get; init; }

    /// <summary>For a refresh (1.5): the video or channel it reads (its YouTube id).</summary>
    public string? Target { get; init; }

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
