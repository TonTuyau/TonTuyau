namespace TonTuyau.Application.YouTube;

/// <summary>The cookie, without its content: what the admin page and the extension are shown.</summary>
public sealed record YouTubeCookieStatus(
    DateTimeOffset ReceivedAt,
    string ReceivedBy,
    YouTubeCookieState State,
    DateTimeOffset? CheckedAt,
    string? Reason)
{
    public static explicit operator YouTubeCookieStatus(YouTubeCookie cookie)
    {
        ArgumentNullException.ThrowIfNull(cookie);
        return new YouTubeCookieStatus(
            cookie.ReceivedAt,
            cookie.ReceivedBy,
            cookie.State,
            cookie.CheckedAt,
            cookie.Reason);
    }
}
