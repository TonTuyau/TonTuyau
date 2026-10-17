using TonTuyau.Application.Store;

namespace TonTuyau.Application.Sync;

public enum SyncMode
{
    Incremental,
    Full,

    /// <summary>
    /// Applies the deletion list a full run wrote but didn't apply (over the limit): a run of its own, since 1.4.
    /// </summary>
    ApplyDeletions,

    /// <summary>Reads one video on YouTube (1.5): its state, and its metadata when it's there.</summary>
    RefreshVideo,

    /// <summary>Reads one channel on YouTube (1.5): its state, and its metadata when it's there.</summary>
    RefreshChannel,

    /// <summary>Reads one channel on YouTube, then each of its videos in the library (1.5).</summary>
    RefreshChannelVideos,
}

public enum SyncTrigger
{
    Scheduler,
    Cli,

    /// <summary>The automatic index rebuild when the backend starts with changed index definitions (QA10 M5-10).</summary>
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

/// <param name="Watermark">
/// Highest TubeArchivist <c>date_downloaded</c> (epoch seconds, TA's own clock) that a completed run indexed.
/// Null until the first completed run.
/// </param>
/// <param name="FullRunRequested">Set by an incremental run that couldn't finish its walk (page-100 net).</param>
public sealed record SyncCheckpoint(long? Watermark, bool FullRunRequested)
{
    public static readonly SyncCheckpoint Empty = new(null, false);
}

/// <summary>Who holds the sync lock, and until when (renewed while the run lives).</summary>
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

    public int Deleted { get; init; }

    public int KeptStillInTubeArchivist { get; init; }

    /// <summary>A refresh (1.5): videos and channels YouTube answered with their metadata.</summary>
    public int FoundOnYouTube { get; init; }

    /// <summary>A refresh: videos and channels YouTube says are removed, or their account terminated.</summary>
    public int GoneFromYouTube { get; init; }

    /// <summary>A refresh: videos YouTube says their owner made private (1.5).</summary>
    public int PrivateOnYouTube { get; init; }

    /// <summary>A refresh: checks without an answer to trust (a bot check, the network…), to try again.</summary>
    public int CheckFailed { get; init; }

    /// <summary>A refresh: the comments read with the videos (answers included).</summary>
    public int CommentsRead { get; init; }
}

/// <summary>One sync run, as kept in the run history.</summary>
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

    /// <summary>For an <see cref="SyncMode.ApplyDeletions"/> run: the full run whose deletion list it applied.</summary>
    public string? AppliesTo { get; init; }

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

    /// <summary>Deletion list file written by a full run (null for incremental runs).</summary>
    public string? DeletionFile { get; init; }

    public int DeletionsPlanned { get; init; }

    public bool DeletionsBlockedByLimit { get; init; }

    /// <summary>The run rebuilt the indices (asked for, or instead of a sync because the definitions changed).</summary>
    public bool IndexRebuild { get; init; }

    public static string NewId(DateTimeOffset now) =>
        $"run-{now.UtcDateTime:yyyyMMdd'T'HHmmss'Z'}-{Guid.NewGuid().ToString("N")[..6]}";
}
