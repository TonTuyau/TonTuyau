using TonTuyau.Application.Library;

namespace TonTuyau.Application.Store;

/// <summary>
/// The library's videos in PostgreSQL (2.0, <c>videos</c>): the source of truth, one set of columns per video. What
/// TubeArchivist brings only ever adds a video TonTuyau doesn't have: a video it has is left as it is, whatever
/// TubeArchivist says of it, so what TonTuyau read on YouTube is never overwritten.
/// </summary>
public interface IVideoStore
{
    /// <summary>
    /// Adds the videos TonTuyau doesn't have, "not checked" on YouTube (TubeArchivist's own flag isn't kept); the
    /// others are left as they are (<see cref="UpsertResult.Unchanged"/>: already there). The same id twice in one
    /// call: the last one counts. Their channels are in the library first (<c>videos.channel_id</c> references
    /// <c>channels</c>); <see cref="Video.ChannelName"/> isn't kept, a read gives the channel's.
    /// </summary>
    /// <param name="origin">Where they come from: TubeArchivist's import, or TonTuyau's own downloads.</param>
    Task<UpsertResult> AddMissingAsync(
        IReadOnlyCollection<Video> videos,
        LibraryOrigin origin,
        CancellationToken cancellationToken);

    Task<Video?> GetAsync(string id, CancellationToken cancellationToken);

    /// <returns>The video with its "file missing on the share" flag, or null when there's none with this id.</returns>
    Task<StoredVideo?> GetStoredAsync(string id, CancellationToken cancellationToken);

    Task<long> CountAsync(CancellationToken cancellationToken);

    /// <returns>Every video of these channels the library has.</returns>
    Task<IReadOnlyList<VideoSummary>> FindByChannelsAsync(
        IReadOnlyCollection<string> channelIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records the file TonTuyau downloaded for a video of the library (B3: its file was missing): its path, size,
    /// tracks and download date, and clears the "file missing" flag; its syncs aren't blocked any more (2.3.0: asked
    /// for, it was downloaded). The rest of the video is left as it is.
    /// </summary>
    /// <returns>False when there's no video with this id.</returns>
    Task<bool> SetFileAsync(string id, VideoFile file, CancellationToken cancellationToken);

    /// <returns>
    /// Every video of the library, whatever its channel: when its file came and whether it's missing (alpha.5: what a
    /// YouTube sync has already, a channel's tab can list another channel's videos).
    /// </returns>
    Task<IReadOnlyList<VideoFileState>> ListFilesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Deletes these videos (2.3.0, a purge): their comments, state on YouTube and keyframes go with them. Unknown ids
    /// are ignored.
    /// </summary>
    /// <returns>How many were deleted.</returns>
    Task<int> DeleteAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken);

    /// <summary>
    /// Blocks these videos' syncs (2.3.0, their files purged: the "file missing" flag set too), or unblocks them (the
    /// flag left: a download gives them a file again). Unknown ids are ignored.
    /// </summary>
    /// <returns>How many changed.</returns>
    Task<int> SetSyncBlockedAsync(IReadOnlyCollection<string> ids, bool blocked, CancellationToken cancellationToken);

    /// <returns>The channel's videos whose syncs are blocked (2.3.0), the latest published first.</returns>
    Task<IReadOnlyList<StoredVideo>> ListSyncBlockedAsync(string channelId, CancellationToken cancellationToken);

    /// <summary>Sets or clears the "file missing on the share" flag. Unknown ids are ignored.</summary>
    Task SetMediaMissingAsync(IReadOnlyCollection<string> ids, bool missing, CancellationToken cancellationToken);

    /// <summary>
    /// Records what the art store holds for these videos' thumbnails (resized versions, QA09). Unknown ids are
    /// ignored.
    /// </summary>
    Task SetThumbnailArtAsync(IReadOnlyDictionary<string, ArtVersions> art, CancellationToken cancellationToken);

    /// <summary>The library's video ids in order, a page at a time (an index rebuild walks them all).</summary>
    /// <param name="after">The previous page's last id; null for the first page.</param>
    /// <returns>At most <paramref name="count"/> ids; fewer at the end, none past it.</returns>
    Task<IReadOnlyList<string>> GetIdsAfterAsync(string? after, int count, CancellationToken cancellationToken);
}
