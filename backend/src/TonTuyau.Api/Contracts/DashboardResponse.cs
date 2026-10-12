using TonTuyau.Application.Dashboard;

namespace TonTuyau.Api.Contracts;

/// <summary>The admin dashboard (contract v1.6): everything its page shows, in one answer.</summary>
/// <param name="LastSync">When the latest successful sync finished; null when there's none in the history read.</param>
/// <param name="Imports">The latest runs that brought new videos, oldest first (at most 30).</param>
/// <param name="Channels">Every channel, biggest first.</param>
public sealed record DashboardResponse(
    DateTime? LastSync,
    LibraryTotals Totals,
    PlayTime PlayTime,
    IReadOnlyList<SyncImport> Imports,
    IReadOnlyList<ChannelTotals> Channels)
{
    public static explicit operator DashboardResponse(DashboardView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new DashboardResponse(
            view.LastSync?.UtcDateTime,
            (LibraryTotals)view.Totals,
            (PlayTime)view.PlayTime,
            [.. view.Imports.Select(i => (SyncImport)i)],
            [.. view.Channels.Select(c => (ChannelTotals)c)]);
    }
}
