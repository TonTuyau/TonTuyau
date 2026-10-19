namespace TonTuyau.Application.YouTube;

/// <summary>What TonTuyau knows of a video or channel on YouTube (1.5).</summary>
/// <param name="CheckedAt">When TonTuyau last asked YouTube; null when it never did.</param>
/// <param name="Reason">What YouTube said when it's gone, or why the check failed; null when on YouTube.</param>
/// <param name="RefreshedAt">When TonTuyau last read its metadata from YouTube; null: never.</param>
public sealed record YouTubeStatus(
    YouTubeState State,
    DateTimeOffset? CheckedAt,
    string? Reason,
    DateTimeOffset? RefreshedAt)
{
    /// <summary>
    /// Before TonTuyau checks, whatever its source: TubeArchivist's own flag isn't kept (2.0), a video it had as
    /// unavailable is not checked like the others.
    /// </summary>
    public static readonly YouTubeStatus NotChecked = new(YouTubeState.NotChecked, null, null, null);
}
