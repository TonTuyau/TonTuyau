namespace TonTuyau.Application.Sync;

public enum SyncMode
{
    Incremental,
    Full,
}

public enum SyncTrigger
{
    Scheduler,
    Cli,

    /// <summary>The automatic index rebuild when the backend starts with changed index definitions (QA10 M5-10).</summary>
    Startup,
}

public enum SyncOutcome
{
    Running,
    Succeeded,
    Failed,
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
}

/// <summary>One sync run, as kept in the run history.</summary>
public sealed record SyncRun
{
    public required string Id { get; init; }

    public required SyncMode Mode { get; init; }

    public required SyncTrigger Trigger { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset? FinishedAt { get; init; }

    public SyncOutcome Outcome { get; init; } = SyncOutcome.Running;

    public string? Error { get; init; }

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
