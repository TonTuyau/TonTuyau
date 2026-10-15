namespace TonTuyau.Application.YouTube;

/// <summary>The YouTube settings (1.5), in PostgreSQL: one row, the defaults until an admin changes them.</summary>
public interface IYouTubeSettingsRepository
{
    Task<YouTubeSettings> GetAsync(CancellationToken cancellationToken);

    Task SaveAsync(YouTubeSettings settings, CancellationToken cancellationToken);
}
