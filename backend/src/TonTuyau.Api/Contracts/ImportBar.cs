using TonTuyau.Application.Dashboard;

namespace TonTuyau.Api.Contracts;

/// <summary>One bar of the import history: the new videos of one run, or of the runs of a day, a week, a month.</summary>
/// <param name="Start">When the step starts (midnight in the sync's time zone), or when the run started.</param>
/// <param name="ByType">The new videos of the runs that recorded their types (TonTuyau 1.2 on).</param>
/// <param name="NotRecorded">The new videos of runs of TonTuyau 1.1 and older, which didn't record the types.</param>
public sealed record ImportBar(DateTime Start, long Total, VideoCounts ByType, long NotRecorded)
{
    public static explicit operator ImportBar(ImportBarView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new ImportBar(
            view.Start.UtcDateTime,
            view.Total,
            new VideoCounts(view.ByType.Videos, view.ByType.Shorts, view.ByType.Streams),
            view.NotRecorded);
    }
}
