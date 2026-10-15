namespace TonTuyau.Application.YouTube;

/// <summary>
/// Whether a video or channel is still on YouTube, as far as TonTuyau knows (1.5). Only <see cref="Gone"/> is ever shown
/// to visitors (the "Unavailable on YouTube" badge): every other state is treated as on YouTube.
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

    /// <summary>YouTube itself said it was removed, made private, or its account terminated.</summary>
    Gone,
}
