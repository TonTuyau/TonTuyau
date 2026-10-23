namespace TonTuyau.Api.Contracts;

/// <param name="Comments">Which comments a refresh reads: <c>off</c>, <c>top</c> or <c>all</c>; required.</param>
/// <param name="CommentsMax">For <c>top</c>: how many, answers included (1 or more); absent: the one kept.</param>
/// <param name="PauseSeconds">Between two videos of a refresh, 0 or more; absent: the one kept.</param>
/// <param name="HoldMinutes">How long a refresh YouTube refused waits, 1 or more; absent: the one kept.</param>
/// <param name="SyncEveryMinutes">
/// How often the incremental YouTube sync runs by itself, in minutes (contract v2.11: it was hours), 0 (by hand only) to
/// 10080 (a week); absent: the one kept.
/// </param>
/// <param name="DownloadFormat">
/// What a download asks yt-dlp for (contract v2.5), in its syntax: one line, 1 to 200 characters; absent: the one kept.
/// </param>
/// <param name="DefaultTabs">
/// The tabs a channel added gets when none is named (contract v2.5), each one given, at least one true; absent: kept.
/// </param>
public sealed record YouTubeSettingsRequest(
    string? Comments,
    int? CommentsMax,
    int? PauseSeconds,
    int? HoldMinutes,
    int? SyncEveryMinutes,
    string? DownloadFormat = null,
    ChannelTabsRequest? DefaultTabs = null);
