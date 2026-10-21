namespace TonTuyau.Application.Dashboard;

/// <summary>
/// Everything the admin dashboard shows but its import history (<see cref="ImportHistoryView"/>) and its channels
/// (alpha.3: a paged list of their own, <see cref="DashboardService.ListChannelsAsync"/>).
/// </summary>
/// <param name="LastSync">When the latest successful import finished, or null when there's none.</param>
public sealed record DashboardView(
    DateTimeOffset? LastSync,
    LibraryTotalsView Totals,
    PlayTimeView PlayTime);
