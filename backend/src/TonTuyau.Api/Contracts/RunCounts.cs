using TonTuyau.Application.Sync;

namespace TonTuyau.Api.Contracts;

/// <summary>A run's counters (contract v1.7): what it read, wrote, copied and deleted.</summary>
/// <param name="Kept">Videos or channels a deletion list named, kept because TubeArchivist still has them.</param>
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
    int Kept)
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
            counts.KeptStillInTubeArchivist);
    }
}
