namespace TonTuyau.Application.YouTube;

/// <summary>
/// Whether a video or channel is still on YouTube, as far as TonTuyau knows (1.5). Visitors see a badge for
/// <see cref="Gone"/>, <see cref="Private"/> and <see cref="Unlisted"/>; the others are treated as on YouTube.
/// </summary>
public enum YouTubeState
{
    /// <summary>TonTuyau hasn't read it on YouTube yet (TubeArchivist had it as active).</summary>
    NotChecked,

    /// <summary>TonTuyau's last check found it on YouTube.</summary>
    OnYouTube,

    /// <summary>
    /// The last check failed (a bot check, the network…), or TubeArchivist had it as unavailable and TonTuyau hasn't
    /// confirmed it: checked again later.
    /// </summary>
    RetryLater,

    /// <summary>
    /// YouTube itself said it was removed (by its owner, for its terms, a copyright claim) or its account terminated:
    /// final, a video gone isn't read again.
    /// </summary>
    Gone,

    /// <summary>Its owner made it private on YouTube: read again at each refresh, it may come back.</summary>
    Private,

    /// <summary>On YouTube, but not listed: only whoever has its link sees it. Read like any video.</summary>
    Unlisted,
}
