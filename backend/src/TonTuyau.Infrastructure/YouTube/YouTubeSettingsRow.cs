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

    /// <summary>How often the incremental YouTube sync runs by itself (2.0), in hours; 0: by hand only.</summary>
    public int SyncEveryHours { get; set; } = YouTubeSettings.DefaultSyncEveryHours;

    /// <summary>The tabs a channel added gets when none is named (2.0, B6): the three by default.</summary>
    public bool DefaultVideos { get; set; } = true;

    public bool DefaultShorts { get; set; } = true;

    public bool DefaultStreams { get; set; } = true;

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
            row.SyncEveryHours,
            new ChannelTabs(row.DefaultVideos, row.DefaultShorts, row.DefaultStreams));
    }
}
