using TonTuyau.Application.Sync;

namespace TonTuyau.Api.Contracts;

/// <summary>A run's counters (contract v1.7; v2.0: nothing is deleted any more): what it read, wrote and copied.</summary>
/// <param name="Unchanged">An import (2.0): what TubeArchivist has that TonTuyau had already, left as it is.</param>
/// <param name="FoundOnYouTube">A refresh (1.5): videos and channels YouTube answered with their metadata.</param>
/// <param name="UnlistedOnYouTube">A refresh: of those found, the videos YouTube has unlisted (contract v1.16).</param>
/// <param name="GoneFromYouTube">A refresh: those YouTube says are removed, or their account terminated.</param>
/// <param name="PrivateOnYouTube">A refresh: those YouTube says their owner made private (contract v1.16).</param>
/// <param name="CheckFailed">
/// A refresh: checks without an answer to trust (a bot check, the network…); a YouTube sync (v2.4): tabs not listed.
/// </param>
/// <param name="DownloadFailed">A YouTube sync (contract v2.4): the videos listed it didn't download, noted each.</param>
/// <param name="CommentsRead">A refresh: the comments read with the videos, answers included.</param>
/// <param name="Subscribed">
/// The channels subscribed to (contract v2.3): TubeArchivist's, by an import, or the one "Add a channel" added.
/// </param>
/// <param name="Purged">A purge (contract v2.17): the videos deleted from TonTuyau, their files with them.</param>
public sealed record RunCounts(
    int VideosRead,
    int ChannelsRead,
    int Created,
    int Updated,
    int Unchanged,
    int Rejected,
    int Corrected,
    int ArtCopied,
    int ArtFailed,
    int ArtResized,
    int ArtResizeFailed,
    int MediaMissing,
    int FoundOnYouTube,
    int UnlistedOnYouTube,
    int GoneFromYouTube,
    int PrivateOnYouTube,
    int CheckFailed,
    int DownloadFailed,
    int CommentsRead,
    int Subscribed,
    int Purged)
{
    public static explicit operator RunCounts(SyncCounts counts)
    {
        ArgumentNullException.ThrowIfNull(counts);
        return new RunCounts(
            counts.VideosRead,
            counts.ChannelsRead,
            counts.Created,
            counts.Updated,
            counts.Unchanged,
            counts.Rejected,
            counts.Corrected,
            counts.ArtCopied,
            counts.ArtFailed,
            counts.ArtResized,
            counts.ArtResizeFailed,
            counts.MediaMissing,
            counts.FoundOnYouTube,
            counts.UnlistedOnYouTube,
            counts.GoneFromYouTube,
            counts.PrivateOnYouTube,
            counts.CheckFailed,
            counts.DownloadFailed,
            counts.CommentsRead,
            counts.Subscribed,
            counts.Purged);
    }
}
