using TonTuyau.Application.Dashboard;

namespace TonTuyau.Api.Contracts;

/// <summary>
/// The admin dashboard (contract v1.6): everything its page shows but the import history and, since alpha.3, the
/// channels (their own calls).
/// </summary>
/// <param name="LastSync">
/// When the latest run that added videos finished (an import or a download, alpha.8); null when none did.
/// </param>
public sealed record DashboardResponse(
    DateTime? LastSync,
    LibraryTotals Totals,
    PlayTime PlayTime)
{
    public static explicit operator DashboardResponse(DashboardView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new DashboardResponse(
            view.LastSync?.UtcDateTime,
            (LibraryTotals)view.Totals,
            (PlayTime)view.PlayTime);
    }
}
