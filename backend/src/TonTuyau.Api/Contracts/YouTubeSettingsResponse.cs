using TonTuyau.Application.YouTube;

namespace TonTuyau.Api.Contracts;

/// <summary>How TonTuyau reads YouTube (contract v1.11, TonTuyau 1.5).</summary>
/// <param name="ReadComments">Whether a refresh reads each video's comments too (up to 500): off by default.</param>
/// <param name="ChangedBy">The admin who last changed them; null while they're the defaults.</param>
public sealed record YouTubeSettingsResponse(bool ReadComments, string? ChangedBy, DateTime? ChangedAt)
{
    public static explicit operator YouTubeSettingsResponse(YouTubeSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new YouTubeSettingsResponse(settings.ReadComments, settings.ChangedBy, settings.ChangedAt?.UtcDateTime);
    }
}
