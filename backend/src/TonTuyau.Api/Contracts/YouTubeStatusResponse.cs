using TonTuyau.Application.YouTube;

namespace TonTuyau.Api.Contracts;

/// <summary>What TonTuyau knows of a video or channel on YouTube (contract v1.12, TonTuyau 1.5).</summary>
/// <param name="State">notChecked | onYouTube | retryLater | gone.</param>
/// <param name="CheckedAt">When TonTuyau last asked YouTube; null when it never did.</param>
/// <param name="Reason">What YouTube said when it's gone, or why the check failed; null otherwise.</param>
/// <param name="RefreshedAt">When TonTuyau last read its metadata on YouTube (channels only; null for videos here).</param>
public sealed record YouTubeStatusResponse(
    YouTubeState State,
    DateTime? CheckedAt,
    string? Reason,
    DateTime? RefreshedAt)
{
    public static explicit operator YouTubeStatusResponse(YouTubeStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        return new YouTubeStatusResponse(
            status.State,
            status.CheckedAt?.UtcDateTime,
            status.Reason,
            status.RefreshedAt?.UtcDateTime);
    }
}
