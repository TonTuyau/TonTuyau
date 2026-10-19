using TonTuyau.Application.Dashboard;

namespace TonTuyau.Api.Contracts;

/// <summary>The admin dashboard (contract v1.6): everything its page shows but the import history (its own call).</summary>
/// <param name="LastSync">When the latest successful import finished; null when there's none.</param>
/// <param name="Channels">Every channel, biggest first.</param>
public sealed record DashboardResponse(
    DateTime? LastSync,
    LibraryTotals Totals,
    PlayTime PlayTime,
    IReadOnlyList<ChannelTotals> Channels)
{
    public static explicit operator DashboardResponse(DashboardView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new DashboardResponse(
            view.LastSync?.UtcDateTime,
            (LibraryTotals)view.Totals,
            (PlayTime)view.PlayTime,
            [.. view.Channels.Select(c => (ChannelTotals)c)]);
    }
}
