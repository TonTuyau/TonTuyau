using Microsoft.Extensions.Logging;
using TonTuyau.Application.Library;
using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;

namespace TonTuyau.Application.YouTube;

/// <summary>
/// The YouTube syncs (2.0, B5): a run over the subscribed channels. Each channel's ticked tabs are listed, newest first
/// (the incremental sync stops at the first video the library has; the full sync, or a channel never synced, reads
/// them whole), then what's to download is, one video at a time, a pause between two, right after its channel. A
/// refusal puts the run on hold from where it was, as a refresh; a video that can't be downloaded is noted and
/// skipped.
/// </summary>
public sealed partial class YouTubeRefreshService
{
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
            return await DownloadVideoAsync(id, read, context).ConfigureAwait(false);
        }
        catch (YouTubeDownloadException ex)
        {
            // Gone, private, members only…: the others are downloaded all the same.
            context.Count(c => c with { DownloadFailed = c.DownloadFailed + 1 });
            context.Note($"{id} not downloaded: {ex.Message}");
            LogNotDownloaded(id, ex.Message);
            return null;
        }
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
        HashSet<string> known = (await videos.FindByChannelsAsync([channelId], ct).ConfigureAwait(false))
            .Select(v => v.Id)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> missingFiles = full
            ? (await videos.FindMissingFilesAsync([channelId], ct).ConfigureAwait(false))
                .ToHashSet(StringComparer.Ordinal)
            : [];
        List<string> wanted = [];
        HashSet<string> taken = new(StringComparer.Ordinal);
        bool everyTab = true;
        foreach (ChannelTab tab in subscription.Tabs.Ticked)
        {
            YouTubeLookup<IReadOnlyList<YouTubeListed>> lookup = await youTube
                .ListTabAsync(channelId, tab, entry => !whole && known.Contains(entry.Id), ct)
                .ConfigureAwait(false);
            if (lookup.Answer == YouTubeAnswer.Refused)
            {
                return (lookup.Reason ?? "YouTube refused", []);
            }

            if (lookup.Item is not { } listed)
            {
                everyTab = false;
                context.Count(c => c with { CheckFailed = c.CheckFailed + 1 });
                context.Note($"\"{name}\": its {Tab(tab)} weren't listed ({lookup.Reason}).");
                LogTabNotListed(channelId, tab, lookup.Reason ?? "");
                continue;
            }

            // A video twice (a stream listed on the videos tab too): once.
            foreach (YouTubeListed entry in listed)
            {
                bool toDownload = !known.Contains(entry.Id) || missingFiles.Contains(entry.Id);
                if (!entry.NotYet && toDownload && taken.Add(entry.Id))
                {
                    wanted.Add(entry.Id);
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

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Channel {ChannelId} listed: {Count} to download (whole: {Whole}).")]
    private partial void LogListed(string channelId, int count, bool whole);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Channel {ChannelId}: its {Tab} tab wasn't listed: {Reason}")]
    private partial void LogTabNotListed(string channelId, ChannelTab tab, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{VideoId} not downloaded: {Reason}")]
    private partial void LogNotDownloaded(string videoId, string reason);
}
