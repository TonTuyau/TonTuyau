namespace TonTuyau.Application.YouTube;

/// <summary>How TonTuyau reads YouTube (1.5), chosen on the admin's YouTube page.</summary>
/// <param name="Comments">Which comments a refresh reads with each video: none by default (reading them is slow).</param>
/// <param name="CommentsMax">
/// For <see cref="YouTubeCommentsChoice.Top"/>: how many, answers included (1 or more). Kept
/// with the other choices, for when Top is chosen again.
/// </param>
/// <param name="PauseSeconds">
/// Between two videos of a refresh: a random wait from half to 1.5 times this (as TubeArchivist does); 0: none.
/// </param>
/// <param name="HoldMinutes">
/// When YouTube refuses (a bot check, too many requests): how long a refresh waits, on hold, before it resumes by itself.
/// </param>
/// <param name="ChangedBy">The admin who last changed them; null while they're the defaults.</param>
/// <param name="DownloadFormat">
/// What a download asks yt-dlp for (2.0), in its own syntax: the best video and the best audio by default (B6 sets it on
/// the page).
/// </param>
/// <param name="SyncEveryHours">
/// How often the incremental YouTube sync runs by itself (2.0, B5), in hours: 6 by default; 0: only by hand.
/// </param>
public sealed record YouTubeSettings(
    YouTubeCommentsChoice Comments,
    int CommentsMax,
    int PauseSeconds,
    int HoldMinutes,
    string? ChangedBy,
    DateTimeOffset? ChangedAt,
    string DownloadFormat = YouTubeSettings.DefaultDownloadFormat,
    int SyncEveryHours = YouTubeSettings.DefaultSyncEveryHours)
{
    public const string DefaultDownloadFormat = "bestvideo+bestaudio";

    public const int DefaultSyncEveryHours = 6;

    /// <summary>A week: a longer wait is better done by hand.</summary>
    public const int MaxSyncEveryHours = 168;

    public const int DefaultCommentsMax = 500;
    public const int DefaultPauseSeconds = 10;
    public const int DefaultHoldMinutes = 60;

    public static readonly YouTubeSettings Default = new(
        YouTubeCommentsChoice.Off,
        DefaultCommentsMax,
        DefaultPauseSeconds,
        DefaultHoldMinutes,
        null,
        null);

    /// <summary>What to ask yt-dlp for; null: no comments.</summary>
    public YouTubeCommentsRequest? CommentsRequest => Comments switch
    {
        YouTubeCommentsChoice.Top => new YouTubeCommentsRequest(CommentsMax),
        YouTubeCommentsChoice.All => new YouTubeCommentsRequest(null),
        _ => null,
    };
}
