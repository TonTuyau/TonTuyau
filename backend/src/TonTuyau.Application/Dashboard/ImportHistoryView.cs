namespace TonTuyau.Application.Dashboard;

/// <summary>The import history of one period.</summary>
/// <param name="TimeZone">The sync's time zone, in which the days, weeks and months are counted.</param>
/// <param name="Bars">Oldest first, every step of the period, empty ones too (one per run for a day: no empty one).</param>
public sealed record ImportHistoryView(ImportStep Step, string TimeZone, IReadOnlyList<ImportBarView> Bars)
{
    public long Total => Bars.Sum(b => b.Total);
}
