using TonTuyau.Application.Dashboard;

namespace TonTuyau.Api.Contracts;

/// <summary>The admin dashboard's import history of one period (contract v1.6).</summary>
/// <param name="Step">What one bar adds up: <c>run</c> (last day), <c>day</c>, <c>week</c> (from Monday), <c>month</c>.</param>
/// <param name="TimeZone">The sync's time zone (IANA id), in which the days, weeks and months are counted.</param>
/// <param name="Total">The new videos of the whole period.</param>
/// <param name="Bars">Oldest first, every step of the period, empty ones too (for the last day: one per run).</param>
public sealed record ImportHistoryResponse(
    ImportStep Step,
    string TimeZone,
    long Total,
    IReadOnlyList<ImportBar> Bars)
{
    public static explicit operator ImportHistoryResponse(ImportHistoryView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new ImportHistoryResponse(
            view.Step,
            view.TimeZone,
            view.Total,
            [.. view.Bars.Select(b => (ImportBar)b)]);
    }
}
