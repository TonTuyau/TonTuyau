namespace TonTuyau.Application.YouTube;

/// <summary>The yt-dlp choice in TonTuyau's database (PostgreSQL): one row at most; reads and writes, no rules.</summary>
public interface IYtdlpSettingsRepository
{
    /// <returns>The settings; the default choice when nothing was stored yet.</returns>
    Task<YtdlpSettings> GetAsync(CancellationToken cancellationToken);

    Task SaveChoiceAsync(YtdlpChoice choice, CancellationToken cancellationToken);

    Task SaveCheckAsync(DateTimeOffset checkedAt, string? failure, CancellationToken cancellationToken);
}
