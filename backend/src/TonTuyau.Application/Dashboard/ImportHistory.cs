using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;

namespace TonTuyau.Application.Dashboard;

/// <summary>
/// The import history's bars: the finished runs' new videos, added up per step. The last day has a bar for every
/// sync, an empty one for a sync that brought nothing (1.2.1: with only the syncs that brought videos, five bars side
/// by side didn't read as 24 hours). The days, weeks (from Monday) and months are those of the sync's time zone, as
/// its crons, so a bar is a day as the admin lives it; every step of those periods has its bar, an empty one too, so
/// that time stays linear along the chart.
/// </summary>
public static class ImportHistory
{
    /// <summary>A year of hourly runs is ~8,700 bars: the longer the period, the longer each bar's step.</summary>
    public static ImportStep StepOf(ImportPeriod period) => period switch
    {
        ImportPeriod.Day => ImportStep.Run,
        ImportPeriod.Week or ImportPeriod.Month => ImportStep.Day,
        ImportPeriod.Year => ImportStep.Week,
        ImportPeriod.All => ImportStep.Month,
        _ => throw new ArgumentOutOfRangeException(nameof(period)),
    };

    /// <returns>When the period starts (the runs to read started from then on); null for every run.</returns>
    public static DateTimeOffset? Since(ImportPeriod period, DateTimeOffset now, TimeZoneInfo zone) =>
        period switch
        {
            ImportPeriod.Day => now.AddHours(-24),
            ImportPeriod.All => null,
            _ => Midnight(FirstStep(period, now, zone), zone),
        };

    /// <param name="runs">The runs of the period (<see cref="Since"/>), in any order.</param>
    public static IReadOnlyList<ImportBarView> Bars(
        ImportPeriod period,
        IEnumerable<SyncRun> runs,
        DateTimeOffset now,
        TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        ImportStep step = StepOf(period);

        // An index rebuild writes the whole library again into new indices: every video counts as new there.
        SyncRun[] finished = runs
            .Where(r => r.FinishedAt is not null && !r.IndexRebuild)
            .OrderBy(r => r.StartedAt)
            .ToArray();
        if (step == ImportStep.Run)
        {
            return [.. finished.Select(r => Bar(r.StartedAt, [r]))];
        }

        SyncRun[] imports = finished
            .Where(r => r.Counts.Created > 0)
            .ToArray();

        // The other periods have a fixed range (shown with empty steps); All starts at the first import's month.
        DateOnly? first = period != ImportPeriod.All
            ? FirstStep(period, now, zone)
            : imports.Length > 0 ? StartOf(step, LocalDay(imports[0].StartedAt, zone)) : null;
        if (first is null)
        {
            // All without any import: no first import, so no range and no bar.
            return [];
        }

        ILookup<DateOnly, SyncRun> bySteps = imports.ToLookup(r => StartOf(step, LocalDay(r.StartedAt, zone)));
        DateOnly last = StartOf(step, LocalDay(now, zone));
        List<ImportBarView> bars = [];
        for (DateOnly day = first.Value; day <= last; day = Next(step, day))
        {
            bars.Add(Bar(Midnight(day, zone), bySteps[day]));
        }

        return bars;
    }

    /// <summary>
    /// Week: today and the 6 days before; month: the last 30 days; year: the weeks of the last 365 days (the same
    /// lengths as the search's <c>published</c> periods).
    /// </summary>
    private static DateOnly FirstStep(ImportPeriod period, DateTimeOffset now, TimeZoneInfo zone)
    {
        DateOnly today = LocalDay(now, zone);
        return period switch
        {
            ImportPeriod.Week => today.AddDays(-6),
            ImportPeriod.Month => today.AddDays(-29),
            ImportPeriod.Year => StartOf(ImportStep.Week, today.AddDays(-364)),
            _ => throw new ArgumentOutOfRangeException(nameof(period)),
        };
    }

    private static ImportBarView Bar(DateTimeOffset start, IEnumerable<SyncRun> runs)
    {
        long videos = 0;
        long shorts = 0;
        long streams = 0;
        long notRecorded = 0;
        foreach (SyncRun run in runs)
        {
            if (run.Counts.CreatedByType is not null)
            {
                videos += run.Counts.CreatedByType.Videos;
                shorts += run.Counts.CreatedByType.Shorts;
                streams += run.Counts.CreatedByType.Streams;
            }
            else
            {
                notRecorded += run.Counts.Created;
            }
        }

        return new ImportBarView(start, new TypeCounts(videos, shorts, streams), notRecorded);
    }

    private static DateOnly LocalDay(DateTimeOffset at, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, zone).DateTime);

    /// <summary>
    /// The day's first instant in the zone. The few zones that change their clocks at midnight get their standard
    /// offset that day (an hour off at worst, on two days a year): not worth more code.
    /// </summary>
    private static DateTimeOffset Midnight(DateOnly day, TimeZoneInfo zone)
    {
        DateTime local = day.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }

    private static DateOnly StartOf(ImportStep step, DateOnly day) => step switch
    {
        ImportStep.Day => day,
        ImportStep.Week => day.AddDays(-(((int)day.DayOfWeek + 6) % 7)),
        ImportStep.Month => new DateOnly(day.Year, day.Month, 1),
        _ => throw new ArgumentOutOfRangeException(nameof(step)),
    };

    private static DateOnly Next(ImportStep step, DateOnly day) => step switch
    {
        ImportStep.Day => day.AddDays(1),
        ImportStep.Week => day.AddDays(7),
        ImportStep.Month => day.AddMonths(1),
        _ => throw new ArgumentOutOfRangeException(nameof(step)),
    };
}
