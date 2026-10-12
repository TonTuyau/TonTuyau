using TonTuyau.Application.Store;

namespace TonTuyau.Application.Dashboard;

/// <summary>One bar of the import history: the new videos of the runs of one step.</summary>
/// <param name="Start">When the step starts (midnight in the sync's time zone), or when the run started.</param>
/// <param name="ByType">The new videos of the runs that recorded their types (TonTuyau 1.2 on).</param>
/// <param name="NotRecorded">The new videos of the runs of TonTuyau 1.1 and older, which didn't record the types.</param>
public sealed record ImportBarView(DateTimeOffset Start, TypeCounts ByType, long NotRecorded)
{
    public long Total => ByType.Videos + ByType.Shorts + ByType.Streams + NotRecorded;
}
