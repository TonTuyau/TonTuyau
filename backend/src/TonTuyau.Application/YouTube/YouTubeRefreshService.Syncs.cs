using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TonTuyau.Application.Library;
using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;

namespace TonTuyau.Application.YouTube;

/// <summary>
/// The YouTube syncs (2.0, B5): a run over the subscribed channels. Each channel's ticked tabs are listed, newest first
/// (the incremental sync stops at the first video the library had at the channel's last sync; the full sync, or a
/// channel never synced, reads them whole), then what's to download is, one video at a time, a pause between two,
/// right after its channel. What the library has is the whole library's, whatever the channel (alpha.5: a channel's
/// tab can list another channel's videos, a collaboration; they were downloaded again at every sync). A refusal puts
/// the run on hold from where it was, as a refresh; a video that can't be downloaded is noted and skipped.
/// </summary>
public sealed partial class YouTubeRefreshService
{
    /// <summary>
    /// Each video a sync listed to download: its channel and title, for its failure if it fails. Taken back at its
    /// download (a run resumed after a restart has none: its failure keeps the channel and title it had).
    /// </summary>
    private readonly ConcurrentDictionary<string, (string ChannelId, string? Title)> _listed =
        new(StringComparer.Ordinal);

    /// <summary>
    /// The library's videos as the sync going on knows them (alpha.5), read once per run (<see cref="_libraryRun"/>)
    /// and kept up to date by its downloads, so a channel listed later sees what an earlier one downloaded.
    /// </summary>
    private Dictionary<string, VideoFileState> _library = new(StringComparer.Ordinal);

    private string? _libraryRun;

    /// <summary>One item of a sync's work: a channel to list (its videos then come after it), or a video to download.</summary>
    /// <param name="full">The full sync: every video of the tabs, and the files missing of those the library has.</param>
    /// <returns>What YouTube said when it refused; null otherwise.</returns>
    private async Task<string?> SyncItemAsync(
        bool full,
        List<string> work,
        int index,
        YouTubeSettings read,
        RunContext context)
    {
        string id = work[index];
        if (LibraryIds.IsChannelId(id))
        {
            context.BeginPhase(SyncPhase.YouTubeChannel);
            (string? refused, IReadOnlyList<string> wanted) = await ListChannelAsync(id, full, context)
                .ConfigureAwait(false);
            if (refused is null)
            {
                work.InsertRange(index + 1, wanted);
            }

            return refused;
        }

        // A pause between two downloads, not after a listing.
        if (index > 0 && LibraryIds.IsVideoId(work[index - 1]))
        {
            await PauseAsync(read.PauseSeconds, context.CancellationToken).ConfigureAwait(false);
        }

        context.BeginPhase(SyncPhase.YouTubeDownload, 1);
        try
        {
            return await DownloadOneAsync(id, read, context).ConfigureAwait(false);
        }
        catch (YouTubeDownloadException ex)
        {
            // Gone, private, members only, no such format…: the others are downloaded all the same.
            context.Count(c => c with { DownloadFailed = c.DownloadFailed + 1 });
            LogNotDownloaded(id, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Downloads a video (<see cref="DownloadVideoAsync"/>); a failure is recorded (B5) when its channel is known: the
    /// one that listed it, the failure before, the library's, or (2.3.0) the one YouTube says.
    /// </summary>
    /// <exception cref="YouTubeDownloadException">Not downloaded: recorded, and noted.</exception>
    /// <exception cref="DiskSpaceLowException">Under the disk space threshold, set to wait (2.2.0): not begun.</exception>
    private async Task<string?> DownloadOneAsync(string videoId, YouTubeSettings read, RunContext context)
    {
        if (await space.WhyWaitAsync(context.CancellationToken).ConfigureAwait(false) is { } wait)
        {
            throw new DiskSpaceLowException(wait);
        }

        _listed.TryRemove(videoId, out (string ChannelId, string? Title) listed);
        // Already there with its file (alpha.5): another channel's sync downloaded it since it was listed, or a failure
        // tried again was downloaded meanwhile. Not again: the download would replace the file with the same.
        StoredVideo? stored = await videos.GetStoredAsync(videoId, context.CancellationToken).ConfigureAwait(false);
        if (stored is not null && !stored.MediaMissing)
        {
            await downloadFailures.RemoveAsync(videoId, context.CancellationToken).ConfigureAwait(false);
            context.Note($"\"{stored.Video.Title}\" ({videoId}) is in the library already: not downloaded again.");
            return null;
        }

        try
        {
            string? refused = await DownloadVideoAsync(videoId, read, context).ConfigureAwait(false);
            if (refused is null && _libraryRun == context.Run.Id)
            {
                _library[videoId] = new VideoFileState(videoId, clock.GetUtcNow(), MediaMissing: false);
            }

            return refused;
        }
        catch (YouTubeDownloadException ex)
        {
            CancellationToken ct = context.CancellationToken;
            DownloadFailure? before = await downloadFailures.GetAsync(videoId, ct).ConfigureAwait(false);
            // The channel that listed it, the one of its failure before, the library's (its file missing), or (2.3.0:
            // "Download a video" of one that never failed) the one YouTube says; none (gone, private…): the run fails,
            // saying why, not recorded.
            (string ChannelId, string? Title)? owner = listed.ChannelId is not null
                ? listed
                : before is not null
                    ? (before.ChannelId, listed.Title)
                    : stored is not null
                        ? (stored.Video.ChannelId, stored.Video.Title)
                        : await OwnerOnYouTubeAsync(videoId, ct).ConfigureAwait(false);
            if (owner is null)
            {
                throw;
            }

            string reason = ex.Reason.Length > 0 ? ex.Reason : ex.Message;
            DownloadFailure failure = await downloadFailures
                .RecordAsync(videoId, owner.Value.ChannelId, owner.Value.Title, reason, clock.GetUtcNow(), ct)
                .ConfigureAwait(false);
            string name = failure.Title is null ? videoId : $"\"{failure.Title}\" ({videoId})";
            string why = reason.TrimEnd('.');
            context.Note(failure.Tries == DownloadFailure.SkippedAfter
                ? $"{name} not downloaded: {why}. Failed {failure.Tries} times: the syncs leave it now."
                : $"{name} not downloaded: {why}. Failed {failure.Tries} time(s).");
            throw;
        }
    }

    /// <summary>
    /// A video's channel and title as YouTube says them (2.3.0), for the failure of a download nothing listed: null when
    /// YouTube doesn't give them (gone, private, no answer).
    /// </summary>
    private async Task<(string ChannelId, string? Title)?> OwnerOnYouTubeAsync(string videoId, CancellationToken ct)
    {
        YouTubeLookup<YouTubeVideo> lookup = await youTube
            .GetVideoAsync(videoId, comments: null, ct)
            .ConfigureAwait(false);
        return lookup is { Answer: YouTubeAnswer.Found, Item: { } video } ? (video.ChannelId, video.Title) : null;
    }

    /// <summary>
    /// Lists a subscribed channel's ticked tabs: the videos the library doesn't have (the full sync: and those whose
    /// file is missing), not those still to come or live now. Marks it synced once each tab was read.
    /// </summary>
    /// <returns>What YouTube said when it refused (nothing to download then), and what to download.</returns>
    private async Task<(string? Refused, IReadOnlyList<string> Wanted)> ListChannelAsync(
        string channelId,
        bool full,
        RunContext context)
    {
        CancellationToken ct = context.CancellationToken;
        ChannelSubscription? subscription = await subscriptions.GetAsync(channelId, ct).ConfigureAwait(false);
        if (subscription is null)
        {
            // Unsubscribed since the run started (or since it was held).
            return (null, []);
        }

        string name = (await channels.GetAsync(channelId, ct).ConfigureAwait(false))?.Name ?? channelId;
        bool whole = full || subscription.SyncedAt is null;
        // When it was listed last: what the library had then is where an incremental listing stops (below it, it
        // listed before). A video another channel downloaded since (a collaboration) doesn't stop it: what this one
        // published before it would be missed.
        DateTimeOffset? syncedBefore = subscription.SyncedAt;
        IReadOnlyDictionary<string, VideoFileState> library = await LibraryAsync(context).ConfigureAwait(false);
        // A video failed too often is left by the incremental sync (question 20); the full one tries it.
        IReadOnlyList<DownloadFailure> failures = await downloadFailures
            .ListAsync(channelId, ct)
            .ConfigureAwait(false);
        Dictionary<string, DownloadFailure> failed = failures.ToDictionary(f => f.VideoId, StringComparer.Ordinal);
        List<string> wanted = [];
        HashSet<string> taken = new(StringComparer.Ordinal);
        bool everyTab = true;
        foreach (ChannelTab tab in subscription.Tabs.Ticked)
        {
            YouTubeLookup<IReadOnlyList<YouTubeListed>> lookup = await youTube
                .ListTabAsync(channelId, tab, entry => !whole && HadBefore(library, entry.Id, syncedBefore), ct)
                .ConfigureAwait(false);
            if (lookup.Answer == YouTubeAnswer.Refused)
            {
                return (lookup.Reason ?? "YouTube refused", []);
            }

            if (lookup.Item is null)
            {
                everyTab = false;
                context.Count(c => c with { CheckFailed = c.CheckFailed + 1 });
                context.Note($"\"{name}\": its {Tab(tab)} weren't listed ({lookup.Reason}).");
                LogTabNotListed(channelId, tab, lookup.Reason ?? "");
                continue;
            }

            // A video twice (a stream listed on the videos tab too): once.
            foreach (YouTubeListed entry in lookup.Item)
            {
                // Not in the library, whatever its channel; the full sync: its file missing too.
                bool toDownload = !library.TryGetValue(entry.Id, out VideoFileState? has) || (full && has.MediaMissing);
                bool leftAlone = !full && failed.GetValueOrDefault(entry.Id)?.Skipped == true;
                if (!entry.NotYet && toDownload && !leftAlone && taken.Add(entry.Id))
                {
                    wanted.Add(entry.Id);
                    _listed[entry.Id] = (channelId, entry.Title);
                }
            }
        }

        context.Count(c => c with { ChannelsRead = c.ChannelsRead + 1 });
        if (everyTab)
        {
            await subscriptions.MarkSyncedAsync(channelId, clock.GetUtcNow(), ct).ConfigureAwait(false);
        }

        LogListed(channelId, wanted.Count, whole);
        if (wanted.Count > 0)
        {
            context.Note(wanted.Count == 1
                ? $"\"{name}\": 1 video to download."
                : $"\"{name}\": {wanted.Count} videos to download.");
        }

        return (null, wanted);
    }

    private static string Tab(ChannelTab tab) => tab.ToString().ToLowerInvariant();

    /// <summary>The library's videos for the sync going on: read at its first channel, then what it downloads.</summary>
    private async Task<IReadOnlyDictionary<string, VideoFileState>> LibraryAsync(RunContext context)
    {
        if (_libraryRun != context.Run.Id)
        {
            IReadOnlyList<VideoFileState> files = await videos
                .ListFilesAsync(context.CancellationToken)
                .ConfigureAwait(false);
            _library = files.ToDictionary(f => f.Id, StringComparer.Ordinal);
            _libraryRun = context.Run.Id;
        }

        return _library;
    }

    /// <summary>Whether the library had this video when the channel was listed last.</summary>
    private static bool HadBefore(
        IReadOnlyDictionary<string, VideoFileState> library,
        string videoId,
        DateTimeOffset? syncedBefore) =>
        syncedBefore is not null
            && library.TryGetValue(videoId, out VideoFileState? had)
            && had.Downloaded < syncedBefore.Value;

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Channel {ChannelId} listed: {Count} to download (whole: {Whole}).")]
    private partial void LogListed(string channelId, int count, bool whole);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Channel {ChannelId}: its {Tab} tab wasn't listed: {Reason}")]
    private partial void LogTabNotListed(string channelId, ChannelTab tab, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{VideoId} not downloaded: {Reason}")]
    private partial void LogNotDownloaded(string videoId, string reason);
}
