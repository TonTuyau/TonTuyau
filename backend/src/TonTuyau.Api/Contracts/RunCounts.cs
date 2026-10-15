using TonTuyau.Application.Sync;

namespace TonTuyau.Api.Contracts;

/// <summary>A run's counters (contract v1.7): what it read, wrote, copied and deleted.</summary>
/// <param name="Kept">Videos or channels a deletion list named, kept because TubeArchivist still has them.</param>
/// <param name="FoundOnYouTube">A refresh (1.5): videos and channels YouTube answered with their metadata.</param>
/// <param name="GoneFromYouTube">A refresh: those YouTube says are removed, private or terminated.</param>
/// <param name="CheckFailed">A refresh: checks without an answer to trust (a bot check, the network…).</param>
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
    int GoneFromYouTube,
    int CheckFailed)
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
            counts.GoneFromYouTube,
            counts.CheckFailed);
    }
}
