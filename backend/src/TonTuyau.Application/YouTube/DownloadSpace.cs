using System.Globalization;
using TonTuyau.Application.Sync;

namespace TonTuyau.Application.YouTube;

/// <summary>
/// Whether the downloads may run (2.2.0): under the YouTube settings' threshold of free space on the videos folder's
/// disk, with the downloads set to wait, they don't. A disk that doesn't say its free space doesn't stop them.
/// </summary>
public sealed class DownloadSpace(IDiskSpace disk, IYouTubeSettingsRepository settings)
{
    public const long BytesPerGb = 1_000_000_000;

    /// <returns>Why the downloads wait now; null when they may run.</returns>
    public async Task<string?> WhyWaitAsync(CancellationToken cancellationToken)
    {
        YouTubeSettings read = await settings.GetAsync(cancellationToken).ConfigureAwait(false);
        if (!read.HoldBelowMinFree || read.MinFreeGb <= 0)
        {
            return null;
        }

        DiskSpace? space = disk.Read();
        return space is null || space.Free >= read.MinFreeGb * BytesPerGb ? null : Why(space, read.MinFreeGb);
    }

    /// <summary>The free space rounded down to a tenth of a GB: just under 50 GB never reads "50 GB".</summary>
    private static string Why(DiskSpace space, int minFreeGb) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Under {minFreeGb} GB free on the videos folder's disk ({Math.Floor(space.Free / (BytesPerGb / 10.0)) / 10:0.#} GB): the downloads wait until space is freed (the YouTube page sets it).");
}
