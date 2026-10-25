using TonTuyau.Application.YouTube;

namespace TonTuyau.Api.Contracts;

/// <summary>How TonTuyau reads YouTube (contract v1.11, TonTuyau 1.5).</summary>
/// <param name="Comments">
/// Which comments a refresh reads with each video: <c>off</c> (the default), <c>top</c> (YouTube's top ones, answers
/// included, up to <paramref name="CommentsMax"/>) or <c>all</c>.
/// </param>
/// <param name="CommentsMax">For <c>top</c>: how many (kept for it while another is chosen).</param>
/// <param name="PauseSeconds">Between two videos of a refresh: a random wait of half to 1.5 times this (10 by default).</param>
/// <param name="HoldMinutes">How long a refresh YouTube refused waits, on hold, before it resumes (60 by default).</param>
/// <param name="SyncEveryMinutes">
/// How often the incremental YouTube sync runs by itself, in minutes (contract v2.11: it was hours; 360 by default); 0:
/// by hand only.
/// </param>
/// <param name="DownloadFormat">
/// What a download asks yt-dlp for (contract v2.5), in its syntax (<c>bestvideo+bestaudio</c> by default).
/// </param>
/// <param name="DefaultTabs">The tabs a channel added gets when none is named (contract v2.5): the three by default.</param>
/// <param name="MinFreeGb">
/// The disk space (contract v2.12): under this many GB free on the videos folder's disk, a warning (50 by default; 0:
/// never).
/// </param>
/// <param name="HoldBelowMinFree">Below it, the downloads don't run (contract v2.12; true by default).</param>
/// <param name="RetryWithoutCookie">
/// A download whose format matched none with the YouTube cookie is tried again without it (contract v2.13; true by
/// default).
/// </param>
/// <param name="ChangedBy">The admin who last changed them; null while they're the defaults.</param>
public sealed record YouTubeSettingsResponse(
    YouTubeCommentsChoice Comments,
    int CommentsMax,
    int PauseSeconds,
    int HoldMinutes,
    int SyncEveryMinutes,
    string DownloadFormat,
    ChannelTabsResponse DefaultTabs,
    int MinFreeGb,
    bool HoldBelowMinFree,
    bool RetryWithoutCookie,
    string? ChangedBy,
    DateTime? ChangedAt)
{
    public static explicit operator YouTubeSettingsResponse(YouTubeSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new YouTubeSettingsResponse(
            settings.Comments,
            settings.CommentsMax,
            settings.PauseSeconds,
            settings.HoldMinutes,
            settings.SyncEveryMinutes,
            settings.DownloadFormat,
            (ChannelTabsResponse)settings.NewChannelTabs,
            settings.MinFreeGb,
            settings.HoldBelowMinFree,
            settings.RetryWithoutCookie,
            settings.ChangedBy,
            settings.ChangedAt?.UtcDateTime);
    }
}
