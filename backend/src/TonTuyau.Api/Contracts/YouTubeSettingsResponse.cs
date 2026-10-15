using TonTuyau.Application.YouTube;

namespace TonTuyau.Api.Contracts;

/// <summary>How TonTuyau reads YouTube (contract v1.11, TonTuyau 1.5).</summary>
/// <param name="Comments">
/// Which comments a refresh reads with each video: <c>off</c> (the default), <c>top</c> (YouTube's top ones, answers
/// included, up to <paramref name="CommentsMax"/>) or <c>all</c>.
/// </param>
/// <param name="CommentsMax">For <c>top</c>: how many (kept for it while another is chosen).</param>
/// <param name="ChangedBy">The admin who last changed them; null while they're the defaults.</param>
public sealed record YouTubeSettingsResponse(
    YouTubeCommentsChoice Comments,
    int CommentsMax,
    string? ChangedBy,
    DateTime? ChangedAt)
{
    public static explicit operator YouTubeSettingsResponse(YouTubeSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new YouTubeSettingsResponse(
            settings.Comments,
            settings.CommentsMax,
            settings.ChangedBy,
            settings.ChangedAt?.UtcDateTime);
    }
}
