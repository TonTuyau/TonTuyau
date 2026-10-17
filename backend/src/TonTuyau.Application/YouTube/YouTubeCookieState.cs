namespace TonTuyau.Application.YouTube;

/// <summary>What the last check of the YouTube cookie said (1.5).</summary>
public enum YouTubeCookieState
{
    /// <summary>Not checked yet, or the check couldn't tell (a bot check, the network): checked again next time.</summary>
    Unchecked,

    /// <summary>YouTube took it: the account's Liked videos could be read with it.</summary>
    Works,

    /// <summary>YouTube didn't take it: signed out, expired, or not a YouTube session.</summary>
    Refused,
}
