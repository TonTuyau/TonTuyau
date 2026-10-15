namespace TonTuyau.Application.YouTube;

/// <summary>What YouTube said about a video or channel TonTuyau asked about (1.5).</summary>
public enum YouTubeAnswer
{
    /// <summary>It's there: its metadata came.</summary>
    Found,

    /// <summary>YouTube says it's gone: removed, private, or its account terminated. Only this is "gone".</summary>
    Gone,

    /// <summary>
    /// No answer to trust: YouTube refused (a bot check, too many requests), the network failed, yt-dlp failed or
    /// isn't there. Try again later; never a reason to call it gone.
    /// </summary>
    CheckFailed,
}
