using TonTuyau.Application.YouTube;

namespace TonTuyau.Api.Contracts;

/// <summary>The YouTube cookie, never its content: who sent it when, and what its last check said.</summary>
/// <param name="State"><c>works</c>, <c>refused</c>, or <c>unchecked</c> (not checked, or the check couldn't tell).</param>
/// <param name="Reason">What YouTube said, when it refused it or the check couldn't tell.</param>
public sealed record YouTubeCookieResponse(
    DateTime ReceivedAt,
    string ReceivedBy,
    YouTubeCookieState State,
    DateTime? CheckedAt,
    string? Reason)
{
    public static explicit operator YouTubeCookieResponse(YouTubeCookieStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        return new YouTubeCookieResponse(
            status.ReceivedAt.UtcDateTime,
            status.ReceivedBy,
            status.State,
            status.CheckedAt?.UtcDateTime,
            status.Reason);
    }
}
