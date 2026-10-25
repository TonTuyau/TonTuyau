using TonTuyau.Application.Library;
using TonTuyau.Application.YouTube;

namespace TonTuyau.Infrastructure.YouTube;

/// <summary>The row of the <c>youtube_settings</c> table (one at most, id 1). The application sees <see cref="YouTubeSettings"/>.</summary>
public sealed class YouTubeSettingsRow
{
    public const int OnlyId = 1;

    public int Id { get; set; } = OnlyId;

    /// <summary>Stored as its name (<c>Top</c>).</summary>
    public YouTubeCommentsChoice Comments { get; set; }

    public int CommentsMax { get; set; } = YouTubeSettings.DefaultCommentsMax;

    public int PauseSeconds { get; set; } = YouTubeSettings.DefaultPauseSeconds;

    public int HoldMinutes { get; set; } = YouTubeSettings.DefaultHoldMinutes;

    public string? ChangedBy { get; set; }

    public DateTimeOffset? ChangedAt { get; set; }

    /// <summary>What a download asks yt-dlp for (2.0), in its syntax.</summary>
    public string DownloadFormat { get; set; } = YouTubeSettings.DefaultDownloadFormat;

    /// <summary>How often the incremental YouTube sync runs by itself, in minutes (2.1.0); 0: by hand only.</summary>
    public int SyncEveryMinutes { get; set; } = YouTubeSettings.DefaultSyncEveryMinutes;

    /// <summary>The tabs a channel added gets when none is named (2.0, B6): the three by default.</summary>
    public bool DefaultVideos { get; set; } = true;

    public bool DefaultShorts { get; set; } = true;

    public bool DefaultStreams { get; set; } = true;

    /// <summary>The disk space (2.2.0): under this many GB free, a warning; 0: never.</summary>
    public int MinFreeGb { get; set; } = YouTubeSettings.DefaultMinFreeGb;

    /// <summary>Below <see cref="MinFreeGb"/>, the downloads don't run (2.2.0).</summary>
    public bool HoldBelowMinFree { get; set; } = true;

    public static explicit operator YouTubeSettings(YouTubeSettingsRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new YouTubeSettings(
            row.Comments,
            row.CommentsMax,
            row.PauseSeconds,
            row.HoldMinutes,
            row.ChangedBy,
            row.ChangedAt,
            row.DownloadFormat,
            row.SyncEveryMinutes,
            new ChannelTabs(row.DefaultVideos, row.DefaultShorts, row.DefaultStreams),
            row.MinFreeGb,
            row.HoldBelowMinFree);
    }
}
