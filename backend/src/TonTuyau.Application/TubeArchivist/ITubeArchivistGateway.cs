using TonTuyau.Application.Library;

namespace TonTuyau.Application.TubeArchivist;

/// <summary>
/// Read-only access to TubeArchivist. Building blocks only (one page, one item, one file): no sync strategy here.
/// Every method may throw a <see cref="TubeArchivistException"/> subtype. A broken document is reported in
/// <see cref="Page{T}.Rejected"/> for lists, and thrown as <see cref="Validation.ValidationException"/> for single items.
/// </summary>
public interface ITubeArchivistGateway
{
    Task<TubeArchivistInfo> PingAsync(CancellationToken cancellationToken);

    /// <summary>True library totals (list endpoints cap their counts at 10,000).</summary>
    Task<LibraryStats> GetLibraryStatsAsync(CancellationToken cancellationToken);

    /// <summary>True number of channels (<c>/api/stats/channel/</c>).</summary>
    Task<long> GetChannelCountAsync(CancellationToken cancellationToken);

    /// <summary>
    /// True number of videos of one channel (<c>/api/channel/&lt;id&gt;/aggs/</c>), not capped at 10k like list totals.
    /// </summary>
    /// <returns>The total; TubeArchivist v0.5.12 answers 0 (not 404) for a channel it doesn't have. Null on a 404.</returns>
    Task<long?> GetChannelVideoTotalAsync(string channelId, CancellationToken cancellationToken);

    /// <param name="page">1-based. A page with nothing on it means past the end.</param>
    Task<Page<Channel>> GetChannelPageAsync(int page, CancellationToken cancellationToken);

    /// <param name="page">1-based. A page with nothing on it means past the end.</param>
    Task<Page<Video>> GetVideoPageAsync(VideoPageQuery query, int page, CancellationToken cancellationToken);

    /// <returns>Null when TubeArchivist doesn't have it.</returns>
    Task<Video?> GetVideoAsync(string videoId, CancellationToken cancellationToken);

    /// <returns>Null when TubeArchivist doesn't have it.</returns>
    Task<Channel?> GetChannelAsync(string channelId, CancellationToken cancellationToken);

    /// <summary>Opens a file from TubeArchivist's cache (thumbnails, channel art). Only <c>/cache/…</c> paths are allowed.</summary>
    /// <returns>Null when TubeArchivist doesn't have it.</returns>
    Task<Stream?> OpenCacheFileAsync(string cachePath, CancellationToken cancellationToken);
}
