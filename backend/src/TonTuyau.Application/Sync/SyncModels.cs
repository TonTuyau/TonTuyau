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
    /// itself (<see cref="YouTube.YouTubeSettings.SyncEveryMinutes"/>), or by hand.
    /// </summary>
    YouTubeSync,

    /// <summary>
    /// The full YouTube sync (2.0, B5): each subscribed channel's ticked tabs, whole; what the library doesn't have is
    /// downloaded, and so are the files missing of the videos it has. By hand only; of one channel alone too
    /// (<see cref="SyncRun.Target"/>: "Download everything" on its page).
    /// </summary>
    YouTubeFullSync,

    /// <summary>
    /// Tries the failed downloads of one channel again (2.0, B5: "Try them all again" on its page,
    /// <see cref="SyncRun.Target"/>), skipped ones included, a pause between two.
    /// </summary>
    RetryDownloads,

    /// <summary>
    /// Refreshes everything from YouTube (2.0, B8): each channel then its videos, the least recently read first, what
    /// was read in the last <see cref="SyncRun.SkipReadDays"/> days skipped; at the refreshes' pace, held on a refusal.
    /// </summary>
    RefreshAll,

    /// <summary>
    /// Purges videos of one channel (2.3.0, <see cref="SyncRun.Target"/>): each one's file, its place in the library,
    /// its comments, failed download and pictures, gone. Its <see cref="SyncRun.Videos"/>, or every video of the
    /// channel when there are none. Nothing is asked of YouTube; a full sync of the channel can bring them back.
    /// </summary>
    PurgeVideos,

    /// <summary>
    /// Refreshes videos of one channel picked on its admin page (2.3.0, <see cref="SyncRun.Target"/>, its
    /// <see cref="SyncRun.Videos"/>): each read on YouTube as <see cref="RefreshVideo"/> reads one; the gone ones
    /// skipped.
    /// </summary>
    RefreshVideos,

    /// <summary>
    /// Downloads videos of one channel again (2.3.0, <see cref="SyncRun.Target"/>, its <see cref="SyncRun.Videos"/>),
    /// with their files: each new file replaces the one there once it's downloaded; one that fails leaves it. Takes
    /// <see cref="SyncRun.DownloadFormat"/> and <see cref="SyncRun.RetryWithoutCookie"/> this once.
    /// </summary>
    RedownloadVideos,
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

    /// <summary>
    /// From the browser extension (2.0, B7: "Add to TonTuyau" on a YouTube page), by the key <see cref="SyncRun.StartedBy"/>.
    /// </summary>
    Extension,
}

/// <summary>Why a run is on hold (2.3.0): only YouTube refusing makes the queue wait.</summary>
public enum HoldReason
{
    /// <summary>YouTube refused (a bot check, too many requests): the next runs would be too; the queue waits.</summary>
    YouTube,

    /// <summary>Put on hold by hand: it waits for Resume now; the queue goes on meanwhile.</summary>
    Hand,

    /// <summary>Under the disk space threshold (2.2.0): it tries again after the cool-down; the queue goes on.</summary>
    DiskSpace,
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

    /// <summary>A purge (2.3.0): the videos it took out of the library, their files deleted.</summary>
    public int Purged { get; init; }
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

    /// <summary>
    /// "Refresh everything" (2.0, B8): what TonTuyau read on YouTube in the last this many days is skipped (0: nothing
    /// is); null for the other runs.
    /// </summary>
    public int? SkipReadDays { get; init; }

    /// <summary>
    /// A download (2.2, "Try again" with a format), a full sync of one channel (2.3.0): yt-dlp's format this once;
    /// null: the YouTube settings' one.
    /// </summary>
    public string? DownloadFormat { get; init; }

    /// <summary>
    /// A full sync of one channel (2.3.0): whether a download whose format matched none with the YouTube cookie is tried
    /// again without it, this once; null: the YouTube settings say.
    /// </summary>
    public bool? RetryWithoutCookie { get; init; }

    /// <summary>
    /// A purge, a refresh or a download again of videos picked (2.3.0): the videos of its channel; empty (a purge
    /// only): every one of them.
    /// </summary>
    public IReadOnlyList<string> Videos { get; init; } = [];

    /// <summary>
    /// A purge (2.3.0): true, only the files go; each video stays, its metadata, thumbnail and comments with it, its
    /// syncs blocked (none downloads it again) and left out of the library's lists until it's unblocked on its
    /// channel's page. False or null: the videos go whole.
    /// </summary>
    public bool? FilesOnly { get; init; }

    /// <summary>"Add a channel" (2.0, B4): the tabs it subscribes to; null for the other runs.</summary>
    public ChannelTabs? Tabs { get; init; }

    /// <summary>A refresh on hold (1.5): when it resumes by itself; null otherwise.</summary>
    public DateTimeOffset? ResumeAt { get; init; }

    /// <summary>A refresh on hold (1.5): the channels and videos left to read, in order (the one refused first).</summary>
    public IReadOnlyList<string> Remaining { get; init; } = [];

    /// <summary>
    /// Why it's on hold (2.3.0); null otherwise, and for a run held before 2.3.0 (taken for YouTube's: the queue waits).
    /// </summary>
    public HoldReason? HeldFor { get; init; }

    /// <summary>
    /// Queued (2.3.0): when it was asked to go first (Run now; Resume now while another run goes on). The runs asked to
    /// go first start before the others, the latest asked first; null: its turn, first come first served.
    /// </summary>
    public DateTimeOffset? FirstAt { get; init; }

    /// <summary>
    /// Queued with what it has left to read (<see cref="Remaining"/>, 2.3.0): a run on hold put back in the queue
    /// (Resume now while another run goes on; Run now of another task while it went on); its turn resumes it.
    /// </summary>
    /// <remarks>A method, not a property: only what it's made of is written with the run.</remarks>
    public bool IsResuming() => Outcome == SyncOutcome.Queued && Remaining.Count > 0;

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
