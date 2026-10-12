namespace TonTuyau.Application.Dashboard;

/// <summary>Everything the admin dashboard shows but its import history (<see cref="ImportHistoryView"/>).</summary>
/// <param name="LastSync">When the latest successful run finished, or null when there's none.</param>
/// <param name="Channels">Biggest first.</param>
public sealed record DashboardView(
    DateTimeOffset? LastSync,
    LibraryTotalsView Totals,
    PlayTimeView PlayTime,
    IReadOnlyList<ChannelTotalsView> Channels);
