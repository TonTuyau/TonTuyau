namespace TonTuyau.Application.Sync;

/// <summary>The steps of a run, in the order they happen. A run goes through some of them only.</summary>
public enum SyncPhase
{
    /// <summary>Full run: TubeArchivist's channel list, with the channels' art.</summary>
    Channels,

    /// <summary>Full run: each channel's videos, with their thumbnails, resized versions and file check.</summary>
    ChannelVideos,

    /// <summary>Incremental run: the newest downloads, page by page, down to the watermark.</summary>
    NewVideos,

    /// <summary>Full run: recording which video files the walk found missing on the media share.</summary>
    MediaCheck,

    /// <summary><c>apply-deletions</c>: which entries of the list TonTuyau still stores.</summary>
    DeletionList,

    /// <summary>The planned deletions, each confirmed with TubeArchivist first.</summary>
    Deletions,

    /// <summary>More deletions planned than the limit allows: none is made.</summary>
    DeletionsOverLimit,

    /// <summary>Art that failed earlier (this run or a previous one), tried again.</summary>
    ArtRetry,

    /// <summary>Index rebuild: switching readers to the new indices.</summary>
    IndexSwitch,
}

/// <summary>
/// Where a running sync is, for whoever watches it (the CLI). In-process only: nothing of it is stored.
/// </summary>
/// <param name="Phase">The current step.</param>
/// <param name="StartedAt">When the command started (for <c>apply-deletions</c>: now, not the run's own start).</param>
/// <param name="Done">
/// How much of the step is done: channel documents read (<see cref="SyncPhase.Channels"/>), channels walked
/// (<see cref="SyncPhase.ChannelVideos"/>), pages read (<see cref="SyncPhase.NewVideos"/>), entries looked up,
/// deletions confirmed, images retried.
/// </param>
/// <param name="Total">The step's end when it's exactly known, else null. Never an estimate.</param>
/// <param name="Counts">The run's counts so far (the ones its report shows).</param>
/// <param name="FilesChecked">Video files looked for on the media share so far.</param>
/// <param name="FilesMissing">Video files not found there so far.</param>
public sealed record SyncProgress(
    SyncPhase Phase,
    DateTimeOffset StartedAt,
    int Done,
    int? Total,
    SyncCounts Counts,
    int FilesChecked,
    int FilesMissing);

/// <summary>
/// Receives a running sync's progress. Called often (per page, per image) and from several threads at once (the art
/// resizing runs in parallel): an implementation must be cheap and thread-safe. Each call carries the whole state.
/// </summary>
public interface ISyncProgressReporter
{
    void Report(SyncProgress progress);
}

/// <summary>Nobody watches: the scheduled and startup runs.</summary>
public sealed class NoSyncProgress : ISyncProgressReporter
{
    public static readonly NoSyncProgress Instance = new();

    private NoSyncProgress()
    {
    }

    public void Report(SyncProgress progress)
    {
    }
}
