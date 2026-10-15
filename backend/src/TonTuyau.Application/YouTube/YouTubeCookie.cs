namespace TonTuyau.Application.YouTube;

/// <summary>
/// The YouTube cookie TonTuyau signs in with (1.5), as stored: the <c>youtube.com</c> lines of a Netscape cookie file,
/// sent by the browser extension. <see cref="Content"/> never leaves the backend except as yt-dlp's cookie file.
/// </summary>
/// <param name="ReceivedBy">Who sent it: "&lt;key name&gt; (API key)", or an account.</param>
/// <param name="Reason">Why the last check refused it or couldn't tell; null when it works or wasn't checked.</param>
public sealed record YouTubeCookie(
    string Content,
    DateTimeOffset ReceivedAt,
    string ReceivedBy,
    YouTubeCookieState State,
    DateTimeOffset? CheckedAt,
    string? Reason);
