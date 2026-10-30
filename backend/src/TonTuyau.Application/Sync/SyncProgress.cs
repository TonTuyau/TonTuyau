namespace TonTuyau.Application.Sync;

/// <summary>The steps of a run, in the order they happen. A run goes through some of them only.</summary>
public enum SyncPhase
{
    /// <summary>Import: TubeArchivist's channel list, the new channels' art.</summary>
    Channels,

    /// <summary>Import: each channel's videos, the new ones' thumbnails, resized versions and file check.</summary>
    ChannelVideos,

    /// <summary>Import: recording which new videos' files the walk found missing on the media share.</summary>
    MediaCheck,

    /// <summary>Art that failed earlier (this run or a previous one), tried again.</summary>
    ArtRetry,

    /// <summary>Index rebuild (2.0): the library's channels and videos written to the new indices from PostgreSQL.</summary>
    Indexing,

    /// <summary>Index rebuild: switching readers to the new indices.</summary>
    IndexSwitch,

    /// <summary>A refresh (1.5): reading the channel on YouTube.</summary>
    YouTubeChannel,

    /// <summary>A refresh: reading the videos on YouTube, one by one.</summary>
    YouTubeVideos,

    /// <summary>A download (2.0): yt-dlp downloading the video, then the library recording it.</summary>
    YouTubeDownload,

    /// <summary>"Refresh everything" (2.0, B8): each channel and video, one by one.</summary>
    YouTubeLibrary,

    /// <summary>A purge (2.3.0): each video deleted, its file and thumbnail with it.</summary>
    Purge,
}

/// <summary>
/// Where a run is while it goes on, for whoever watches it (the CLI). In-process only: nothing of it is stored.
/// </summary>
/// <param name="Phase">The current step.</param>
/// <param name="StartedAt">When the command started.</param>
/// <param name="Done">
/// How much of the step is done: channel documents read (<see cref="SyncPhase.Channels"/>), channels walked
/// (<see cref="SyncPhase.ChannelVideos"/>), rows indexed, images retried.
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
/// Receives a run's progress while it goes on. Called often (per page, per image) and from several threads at once
/// (the art resizing runs in parallel): an implementation must be cheap and thread-safe. Each call carries the whole
/// state.
/// </summary>
public interface ISyncProgressReporter
{
    void Report(SyncProgress progress);

    /// <summary>Told once, when the run has started (its record exists), with its id.</summary>
    void Started(string runId)
    {
    }
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
