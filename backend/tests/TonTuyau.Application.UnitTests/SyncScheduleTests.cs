using TonTuyau.Application.Configuration;
using TonTuyau.Application.Sync;

namespace TonTuyau.Application.UnitTests;

public sealed class SyncScheduleTests
{
    private static readonly TimeZoneInfo Paris = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");

    private static readonly SyncSchedule Default = new(new SyncOptions(
        TonTuyauOptionsReader.DefaultIncrementalCron,
        TonTuyauOptionsReader.DefaultFullCron,
        "Europe/Paris"));

    [Theory]
    [InlineData("2026-10-02 23:30", "2026-10-03 01:00", SyncMode.Full)] // Friday night → the weekly full run
    [InlineData("2026-10-03 01:00", "2026-10-03 07:00", SyncMode.Incremental)] // just after it: first daytime run
    [InlineData("2026-10-03 06:59", "2026-10-03 07:00", SyncMode.Incremental)]
    [InlineData("2026-10-06 12:15", "2026-10-06 13:00", SyncMode.Incremental)]
    [InlineData("2026-10-04 23:30", "2026-10-05 07:00", SyncMode.Incremental)] // no sync between 23:00 and 07:00
    [InlineData("2026-03-28 23:30", "2026-03-29 07:00", SyncMode.Incremental)] // night of the spring DST change
    [InlineData("2026-10-24 01:30", "2026-10-24 07:00", SyncMode.Incremental)] // Saturday before the autumn change
    public void Next_run_in_Paris_time(string after, string expected, SyncMode mode)
    {
        (DateTimeOffset At, SyncMode Mode)? next = Default.Next(ParisTime(after));

        Assert.Equal((ParisTime(expected), mode), next);
    }

    [Fact]
    public void When_both_are_due_together_the_full_run_wins()
    {
        SyncSchedule schedule = new(new SyncOptions("0 1 * * *", "0 1 * * 6", "Europe/Paris"));

        Assert.Equal((ParisTime("2026-10-03 01:00"), SyncMode.Full), schedule.Next(ParisTime("2026-10-02 12:00")));
        Assert.Equal(
            (ParisTime("2026-10-04 01:00"), SyncMode.Incremental),
            schedule.Next(ParisTime("2026-10-03 01:00")));
    }

    // Exhaustive over a week, on the hour and at half past, against a minute-by-minute reference in Paris time.
    [Theory, CombinatorialData]
    public void Default_schedule_matches_a_brute_force_reference(
        [CombinatorialRange(0, 168)] int hour,
        [CombinatorialValues(0, 30)] int minute)
    {
        DateTimeOffset start = ParisTime("2026-10-05 00:00").AddHours(hour).AddMinutes(minute);

        Assert.Equal(Reference(start), Default.Next(start));
    }

    private static (DateTimeOffset, SyncMode) Reference(DateTimeOffset after)
    {
        for (DateTimeOffset t = after.AddMinutes(1); ; t = t.AddMinutes(1))
        {
            DateTimeOffset local = TimeZoneInfo.ConvertTime(t, Paris);
            if (local.Minute != 0)
            {
                continue;
            }

            if (local.DayOfWeek == DayOfWeek.Saturday && local.Hour == 1)
            {
                return (t, SyncMode.Full);
            }

            if (local.Hour is >= 7 and <= 23)
            {
                return (t, SyncMode.Incremental);
            }
        }
    }

    private static DateTimeOffset ParisTime(string local)
    {
        DateTime time = DateTime.ParseExact(
            local,
            "yyyy-MM-dd HH:mm",
            System.Globalization.CultureInfo.InvariantCulture);
        return new DateTimeOffset(time, Paris.GetUtcOffset(time));
    }
}
