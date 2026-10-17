namespace TonTuyau.Application.YouTube;

/// <summary>The cookie's content for yt-dlp, read when a run starts (1.5); null when there's none.</summary>
public interface IYouTubeCookieSource
{
    Task<string?> GetAsync(CancellationToken cancellationToken);
}
