using TonTuyau.Application.Library;

namespace TonTuyau.Application.Store;

/// <summary>The videos the YouTube syncs couldn't download (2.0, B5), in PostgreSQL (<c>download_failures</c>).</summary>
public interface IDownloadFailureStore
{
    /// <summary>
    /// A try failed: recorded the first time, else one more try with its reason (and its title, when given now).
    /// </summary>
    /// <returns>The failure as it is now.</returns>
    Task<DownloadFailure> RecordAsync(
        string videoId,
        string channelId,
        string? title,
        string reason,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    Task<DownloadFailure?> GetAsync(string videoId, CancellationToken cancellationToken);

    /// <returns>A channel's failures, the latest failed first.</returns>
    Task<IReadOnlyList<DownloadFailure>> ListAsync(string channelId, CancellationToken cancellationToken);

    /// <summary>It's downloaded now. Ignored when it never failed.</summary>
    Task RemoveAsync(string videoId, CancellationToken cancellationToken);
}
