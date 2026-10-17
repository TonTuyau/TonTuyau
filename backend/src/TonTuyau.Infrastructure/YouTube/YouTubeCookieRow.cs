using TonTuyau.Application.YouTube;

namespace TonTuyau.Infrastructure.YouTube;

/// <summary>
/// The row of the <c>youtube_cookie</c> table (one at most, id 1), as EF Core maps it. The application sees a
/// <see cref="YouTubeCookie"/>.
/// </summary>
public sealed class YouTubeCookieRow
{
    public const int OnlyId = 1;

    public int Id { get; set; } = OnlyId;

    /// <summary>The <c>youtube.com</c> lines of a Netscape cookie file: a YouTube session, never logged or shown.</summary>
    public required string Content { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }

    public required string ReceivedBy { get; set; }

    /// <summary>Stored as its name (<c>Works</c>).</summary>
    public YouTubeCookieState State { get; set; }

    public DateTimeOffset? CheckedAt { get; set; }

    public string? Reason { get; set; }

    public static explicit operator YouTubeCookie(YouTubeCookieRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new YouTubeCookie(row.Content, row.ReceivedAt, row.ReceivedBy, row.State, row.CheckedAt, row.Reason);
    }
}
