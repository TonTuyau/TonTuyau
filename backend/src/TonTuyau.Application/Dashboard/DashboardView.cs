namespace TonTuyau.Application.Dashboard;

/// <summary>
/// Everything the admin dashboard shows but its import history (<see cref="ImportHistoryView"/>) and its channels
/// (alpha.3: a paged list of their own, <see cref="DashboardService.ListChannelsAsync"/>).
/// </summary>
/// <param name="LastSync">When the latest run that added videos finished (an import or a download, alpha.8), or null.</param>
public sealed record DashboardView(
    DateTimeOffset? LastSync,
    LibraryTotalsView Totals,
    PlayTimeView PlayTime);
