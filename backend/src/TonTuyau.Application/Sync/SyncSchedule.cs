using Cronos;
using TonTuyau.Application.Configuration;

namespace TonTuyau.Application.Sync;

/// <summary>
/// When the scheduler runs what: the incremental and full crons evaluated in the configured time zone.
/// When both are due at the same instant, the full run wins (it does everything the incremental would).
/// </summary>
public sealed class SyncSchedule
{
    private readonly CronExpression _incremental;
    private readonly CronExpression _full;
    private readonly TimeZoneInfo _zone;

    public SyncSchedule(SyncOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _incremental = CronExpression.Parse(options.IncrementalCron, CronFormat.Standard);
        _full = CronExpression.Parse(options.FullCron, CronFormat.Standard);
        _zone = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZone);
    }

    /// <returns>The next run strictly after <paramref name="after"/>, or null if neither cron ever fires again.</returns>
    public (DateTimeOffset At, SyncMode Mode)? Next(DateTimeOffset after)
    {
        DateTimeOffset? incremental = _incremental.GetNextOccurrence(after, _zone);
        DateTimeOffset? full = _full.GetNextOccurrence(after, _zone);
        if (full is { } f && (incremental is null || f <= incremental))
        {
            return (f, SyncMode.Full);
        }

        return incremental is { } i ? (i, SyncMode.Incremental) : null;
    }
}
