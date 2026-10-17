using TonTuyau.Application.Sync;

namespace TonTuyau.Api.Contracts;

/// <summary>A run's counters (contract v1.7): what it read, wrote, copied and deleted.</summary>
/// <param name="Kept">Videos or channels a deletion list named, kept because TubeArchivist still has them.</param>
/// <param name="FoundOnYouTube">A refresh (1.5): videos and channels YouTube answered with their metadata.</param>
/// <param name="UnlistedOnYouTube">A refresh: of those found, the videos YouTube has unlisted (contract v1.16).</param>
/// <param name="GoneFromYouTube">A refresh: those YouTube says are removed, or their account terminated.</param>
/// <param name="PrivateOnYouTube">A refresh: those YouTube says their owner made private (contract v1.16).</param>
/// <param name="CheckFailed">A refresh: checks without an answer to trust (a bot check, the network…).</param>
/// <param name="CommentsRead">A refresh: the comments read with the videos, answers included.</param>
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
    int Deleted,
    int Kept,
    int FoundOnYouTube,
    int UnlistedOnYouTube,
    int GoneFromYouTube,
    int PrivateOnYouTube,
    int CheckFailed,
    int CommentsRead)
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
            counts.Deleted,
            counts.KeptStillInTubeArchivist,
            counts.FoundOnYouTube,
            counts.UnlistedOnYouTube,
            counts.GoneFromYouTube,
            counts.PrivateOnYouTube,
            counts.CheckFailed,
            counts.CommentsRead);
    }
}
