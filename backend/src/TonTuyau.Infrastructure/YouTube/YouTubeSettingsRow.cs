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

    public static explicit operator YouTubeSettings(YouTubeSettingsRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new YouTubeSettings(
            row.Comments,
            row.CommentsMax,
            row.PauseSeconds,
            row.HoldMinutes,
            row.ChangedBy,
            row.ChangedAt);
    }
}
