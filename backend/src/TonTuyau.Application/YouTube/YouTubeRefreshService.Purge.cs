using System.Globalization;
using Microsoft.Extensions.Logging;
using TonTuyau.Application.Library;
using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;

namespace TonTuyau.Application.YouTube;

/// <summary>
/// The purge (2.3.0): videos of one channel deleted from TonTuyau, one at a time: the file, the thumbnail and its resized
/// versions, the failed download, then the video (its comments, state on YouTube and keyframes go with it) and its copy
/// in the index. Nothing is asked of YouTube: the channel subscribed to, its next sync reads its tabs whole and downloads
/// them again; an import brings back TubeArchivist's. Its files only: each video stays, its metadata, thumbnail and
/// comments with it, its syncs blocked and left out of the library's lists until it's unblocked.
/// </summary>
public sealed partial class YouTubeRefreshService
{
    private const double BytesPerGb = 1024d * 1024 * 1024;

    /// <summary>
    /// The videos picked on a channel's page (2.3.0): those asked for that are the channel's, or all of them; latest
    /// first. One gone from the library meanwhile is left out.
    /// </summary>
    /// <param name="asked">The videos asked for; none: the whole channel.</param>
    private async Task<IReadOnlyList<VideoSummary>> PickedAsync(
        string channelId,
        IReadOnlyList<string> asked,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<VideoSummary> ofChannel = await videos
            .FindByChannelsAsync([channelId], cancellationToken)
            .ConfigureAwait(false);
        HashSet<string> wanted = new(asked, StringComparer.Ordinal);
        return
        [
            .. ofChannel
                .Where(v => wanted.Count == 0 || wanted.Contains(v.Id))
                .OrderByDescending(v => v.Downloaded),
        ];
    }

    /// <summary>
    /// Downloads a video of the library again (2.3.0), with the run's format: the new file replaces the one there once
    /// it's downloaded (an old one elsewhere deleted then); one that fails leaves it, noted, and the others go on.
    /// </summary>
    /// <returns>What YouTube said when it refused; null otherwise.</returns>
    /// <exception cref="DiskSpaceLowException">Under the disk space threshold, set to wait (2.2.0): not begun.</exception>
    private async Task<string?> RedownloadOneAsync(string videoId, YouTubeSettings read, RunContext context)
    {
        CancellationToken ct = context.CancellationToken;
        string? wait = await space.WhyWaitAsync(ct).ConfigureAwait(false);
        if (wait is not null)
        {
            throw new DiskSpaceLowException(wait);
        }

        StoredVideo? stored = await videos.GetStoredAsync(videoId, ct).ConfigureAwait(false);
        if (stored is null)
        {
            // Purged meanwhile.
            return null;
        }

        string? refused;
        try
        {
            refused = await DownloadVideoAsync(videoId, read, context).ConfigureAwait(false);
        }
        catch (YouTubeDownloadException ex)
        {
            string reason = ex.Reason.Length > 0 ? ex.Reason : ex.Message;
            context.Count(c => c with { DownloadFailed = c.DownloadFailed + 1 });
            context.Note(
                $"\"{stored.Video.Title}\" ({videoId}) not downloaded again: {reason.TrimEnd('.')}. Its file stays.");
            LogNotDownloaded(videoId, ex.Message);
            return null;
        }

        // Its file placed at its channel's: one elsewhere before (imported from TubeArchivist, another channel's
        // folder) is the old one.
        string old = stored.Video.MediaPath;
        if (refused is null && !stored.MediaMissing && old != $"{stored.Video.ChannelId}/{videoId}.mkv")
        {
            try
            {
                media.Delete(old);
            }
            catch (IOException ex)
            {
                context.Note($"\"{stored.Video.Title}\" ({videoId}): its old file {old} stays ({ex.Message}).");
            }
        }

        return refused;
    }

    /// <summary>
    /// Deletes one video of the library, with its file; or its file only, its syncs blocked. One gone meanwhile is left,
    /// and so is one already blocked when its file only is asked for (not counted again).
    /// </summary>
    /// <returns>The bytes its file freed; 0 when it had none on the share.</returns>
    private async Task<long> PurgeOneAsync(string videoId, bool filesOnly, RunContext context)
    {
        CancellationToken ct = context.CancellationToken;
        StoredVideo? stored = await videos.GetStoredAsync(videoId, ct).ConfigureAwait(false);
        if (stored is null || (filesOnly && stored.SyncBlocked))
        {
            return 0;
        }

        Video video = stored.Video;
        bool deleted;
        try
        {
            deleted = media.Delete(video.MediaPath);
        }
        catch (IOException ex)
        {
            // Its file stays, so does the video: nothing is lost track of.
            context.Note($"\"{video.Title}\" ({videoId}) not purged: its file can't be deleted ({ex.Message}).");
            LogNotPurged(videoId, ex.Message);
            return 0;
        }

        await downloadFailures.RemoveAsync(videoId, ct).ConfigureAwait(false);
        if (filesOnly)
        {
            await videos.SetSyncBlockedAsync([videoId], blocked: true, ct).ConfigureAwait(false);
        }
        else
        {
            if (LibraryPaths.IsArtPath(video.ThumbnailPath))
            {
                art.Delete(video.ThumbnailPath);
            }

            await videos.DeleteAsync([videoId], ct).ConfigureAwait(false);
        }

        context.Count(c => c with { Purged = c.Purged + 1 });
        LogPurged(videoId, video.Title, deleted, filesOnly);
        return deleted ? video.MediaSize : 0;
    }

    /// <summary>
    /// Says what the purge did. The videos gone whole, the channel subscribed to: its next sync reads its tabs whole
    /// (an incremental one stops at the videos it had, the purged ones would be missed) and downloads them again.
    /// </summary>
    private async Task AfterPurgeAsync(long freed, RunContext context)
    {
        int purged = context.Run.Counts.Purged;
        string what = string.Create(
            CultureInfo.InvariantCulture,
            $"{purged} {(purged == 1 ? "video" : "videos")} purged, {freed / BytesPerGb:0.##} GB freed on the videos folder's disk.");
        if (context.Run.FilesOnly == true)
        {
            context.Note(
                $"{what} Their files only: no sync downloads them again until they're unblocked on the channel's page.");
            return;
        }

        string channelId = context.Run.Target!;
        bool subscribed = await subscriptions
            .GetAsync(channelId, context.CancellationToken)
            .ConfigureAwait(false) is not null;
        if (subscribed && purged > 0)
        {
            await subscriptions.ForgetSyncedAsync(channelId, context.CancellationToken).ConfigureAwait(false);
        }

        context.Note(subscribed
            ? $"{what} The channel's next sync downloads them again."
            : $"{what} The channel isn't subscribed to: \"Download everything\" downloads them again once it is.");
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "{VideoId} purged: \"{Title}\" (its file deleted: {File}; the file only: {FilesOnly}).")]
    private partial void LogPurged(string videoId, string title, bool file, bool filesOnly);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{VideoId} not purged: its file can't be deleted: {Reason}")]
    private partial void LogNotPurged(string videoId, string reason);
}
