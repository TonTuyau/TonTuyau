namespace TonTuyau.Application.YouTube;

/// <summary>What YouTube said about a video or channel TonTuyau asked about (1.5).</summary>
public enum YouTubeAnswer
{
    /// <summary>It's there: its metadata came.</summary>
    Found,

    /// <summary>YouTube says it's gone: removed, or its account terminated. Only this is "gone".</summary>
    Gone,

    /// <summary>YouTube says it's private: its owner may make it public again.</summary>
    Private,

    /// <summary>
    /// No answer to trust: the network failed, yt-dlp failed or isn't there, a country block, an age check… Try again
    /// later; never a reason to call it gone.
    /// </summary>
    CheckFailed,

    /// <summary>
    /// YouTube refuses TonTuyau for now: a bot check, or too many requests. Asking again at once only makes it worse: a
    /// refresh then waits (it's put on hold). Never a reason to call it gone either.
    /// </summary>
    Refused,
}
