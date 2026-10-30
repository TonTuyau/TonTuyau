using TonTuyau.Application.Library;

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
/// <param name="SyncEveryMinutes">
/// How often the incremental YouTube sync runs by itself (2.0, B5), in minutes since 2.1.0 (it was hours), for whoever
/// wants it eager: 6 hours by default; 0: only by hand.
/// </param>
/// <param name="DefaultTabs">
/// The tabs a channel added is subscribed to when none is named (2.0, B6: "Add a channel", the browser extension); null:
/// the three (<see cref="NewChannelTabs"/>).
/// </param>
/// <param name="MinFreeGb">
/// The disk space (2.2.0): under this many GB free on the videos folder's disk, the dashboard warns (50 by default; 0:
/// never).
/// </param>
/// <param name="HoldBelowMinFree">
/// Below <paramref name="MinFreeGb"/>, the downloads don't run (2.2.0, on by default): a download asked for is refused,
/// a sync going on waits on hold.
/// </param>
/// <param name="RetryWithoutCookie">
/// A download whose format matched none of the video's, signed in with the YouTube cookie, is tried again at once
/// without it (2.3.0, on by default): signed in, YouTube can keep its formats from yt-dlp but one.
/// </param>
public sealed record YouTubeSettings(
    YouTubeCommentsChoice Comments,
    int CommentsMax,
    int PauseSeconds,
    int HoldMinutes,
    string? ChangedBy,
    DateTimeOffset? ChangedAt,
    string DownloadFormat = YouTubeSettings.DefaultDownloadFormat,
    int SyncEveryMinutes = YouTubeSettings.DefaultSyncEveryMinutes,
    ChannelTabs? DefaultTabs = null,
    int MinFreeGb = YouTubeSettings.DefaultMinFreeGb,
    bool HoldBelowMinFree = true,
    bool RetryWithoutCookie = true)
{
    public const int DefaultMinFreeGb = 50;

    /// <summary>A petabyte: a bigger threshold is a typo.</summary>
    public const int MaxMinFreeGb = 1_000_000;

    /// <summary>A format longer than this isn't one (yt-dlp's are a few dozen characters).</summary>
    public const int MaxDownloadFormatLength = 200;

    public const string DefaultDownloadFormat = "bestvideo+bestaudio";

    /// <summary>What yt-dlp's format must be, said the same wherever one is given.</summary>
    public static readonly string DownloadFormatRule =
        $"must be yt-dlp's format, on one line, 1 to {MaxDownloadFormatLength} characters.";

    /// <summary>Whether a format, trimmed, can be one: on one line, 1 to <see cref="MaxDownloadFormatLength"/> long.</summary>
    public static bool IsDownloadFormat(string format) =>
        format.Length is > 0 and <= MaxDownloadFormatLength && !format.Any(char.IsControl);

    public const int DefaultSyncEveryMinutes = 6 * 60;

    /// <summary>A week: a longer wait is better done by hand.</summary>
    public const int MaxSyncEveryMinutes = 7 * 24 * 60;

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

    /// <summary>The tabs a channel added is subscribed to when none is named: the three unless set.</summary>
    public ChannelTabs NewChannelTabs => DefaultTabs ?? ChannelTabs.All;

    /// <summary>What to ask yt-dlp for; null: no comments.</summary>
    public YouTubeCommentsRequest? CommentsRequest => Comments switch
    {
        YouTubeCommentsChoice.Top => new YouTubeCommentsRequest(CommentsMax),
        YouTubeCommentsChoice.All => new YouTubeCommentsRequest(null),
        _ => null,
    };
}
