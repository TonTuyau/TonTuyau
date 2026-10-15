using TonTuyau.Application.YouTube;

namespace TonTuyau.Infrastructure.YouTube;

/// <summary>The row of the <c>youtube_settings</c> table (one at most, id 1). The application sees <see cref="YouTubeSettings"/>.</summary>
public sealed class YouTubeSettingsRow
{
    public const int OnlyId = 1;

    public int Id { get; set; } = OnlyId;

    public bool ReadComments { get; set; }

    public string? ChangedBy { get; set; }

    public DateTimeOffset? ChangedAt { get; set; }

    public static explicit operator YouTubeSettings(YouTubeSettingsRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new YouTubeSettings(row.ReadComments, row.ChangedBy, row.ChangedAt);
    }
}
