namespace TonTuyau.Application.Dashboard;

/// <summary>Everything the admin dashboard shows.</summary>
/// <param name="LastSync">When the latest successful run finished, or null when there's none among the runs read.</param>
/// <param name="Imports">Oldest first, as the chart reads them left to right.</param>
/// <param name="Channels">Biggest first.</param>
public sealed record DashboardView(
    DateTimeOffset? LastSync,
    LibraryTotalsView Totals,
    PlayTimeView PlayTime,
    IReadOnlyList<ImportView> Imports,
    IReadOnlyList<ChannelTotalsView> Channels);
