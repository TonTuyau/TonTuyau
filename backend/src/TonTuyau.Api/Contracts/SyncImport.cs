using TonTuyau.Application.Dashboard;

namespace TonTuyau.Api.Contracts;

/// <summary>One sync run that brought new videos.</summary>
/// <param name="At">When the run started.</param>
/// <param name="Total">How many new videos it brought.</param>
/// <param name="ByType">Null for runs of TonTuyau 1.1 and older, which didn't record the types.</param>
public sealed record SyncImport(DateTime At, int Total, VideoCounts? ByType)
{
    public static explicit operator SyncImport(ImportView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new SyncImport(
            view.At.UtcDateTime,
            view.Total,
            view.ByType is { } counts ? new VideoCounts(counts.Videos, counts.Shorts, counts.Streams) : null);
    }
}
