namespace TonTuyau.Application.YouTube;

/// <summary>The YouTube cookie in TonTuyau's database (PostgreSQL): one at most; reads and writes, no rules.</summary>
public interface IYouTubeCookieRepository
{
    Task<YouTubeCookie?> GetAsync(CancellationToken cancellationToken);

    /// <summary>Stores a new cookie in place of the one there was, not checked yet.</summary>
    Task SaveAsync(string content, DateTimeOffset receivedAt, string receivedBy, CancellationToken cancellationToken);

    /// <summary>Notes a check of the cookie stored; nothing when there's none.</summary>
    Task SetCheckAsync(
        YouTubeCookieState state,
        DateTimeOffset checkedAt,
        string? reason,
        CancellationToken cancellationToken);

    /// <returns>Whether there was one.</returns>
    Task<bool> DeleteAsync(CancellationToken cancellationToken);
}
