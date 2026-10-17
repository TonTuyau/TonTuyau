using TonTuyau.Application.YouTube;

namespace TonTuyau.Infrastructure.YouTube;

/// <summary>
/// The row of the <c>ytdlp_settings</c> table (one at most, id 1), as EF Core maps it: the yt-dlp chosen, and the last
/// check. The application sees <see cref="YtdlpSettings"/>.
/// </summary>
public sealed class YtdlpSettingsRow
{
    public const int OnlyId = 1;

    public int Id { get; set; } = OnlyId;

    /// <summary>Stored as its name (<c>Latest</c>).</summary>
    public YtdlpChoiceKind Kind { get; set; }

    public string? Version { get; set; }

    public string? ChosenBy { get; set; }

    public DateTimeOffset? ChosenAt { get; set; }

    public DateTimeOffset? CheckedAt { get; set; }

    public string? Error { get; set; }

    public static explicit operator YtdlpSettings(YtdlpSettingsRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new YtdlpSettings(
            new YtdlpChoice(row.Kind, row.Version, row.ChosenBy, row.ChosenAt),
            row.CheckedAt,
            row.Error);
    }
}
