namespace TonTuyau.Application.YouTube;

/// <summary>What TonTuyau knows of a video or channel on YouTube (1.5).</summary>
/// <param name="CheckedAt">When TonTuyau last asked YouTube; null when it never did.</param>
/// <param name="Reason">What YouTube said when it's gone, or why the check failed; null when on YouTube.</param>
/// <param name="RefreshedAt">
/// When TonTuyau last read its metadata from YouTube: from then on, that metadata wins over TubeArchivist's.
/// </param>
public sealed record YouTubeStatus(
    YouTubeState State,
    DateTimeOffset? CheckedAt,
    string? Reason,
    DateTimeOffset? RefreshedAt)
{
    public const string FromTubeArchivistUnavailable =
        "TubeArchivist had it as unavailable; TonTuyau hasn't confirmed it yet.";

    /// <summary>
    /// Before TonTuyau checks: TubeArchivist's "active" is not checked yet; its "inactive" is to retry, never gone (its
    /// flag was set when a refresh failed for any reason).
    /// </summary>
    public static YouTubeStatus FromTubeArchivist(bool active) =>
        active
            ? new YouTubeStatus(YouTubeState.NotChecked, null, null, null)
            : new YouTubeStatus(YouTubeState.RetryLater, null, FromTubeArchivistUnavailable, null);
}
