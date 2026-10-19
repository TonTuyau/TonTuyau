using TonTuyau.Application.Sync;

namespace TonTuyau.Api.Contracts;

/// <summary>A run is going on, in this backend or another process (the run lock): no other can start.</summary>
/// <param name="Task">import | indexRebuild | refreshVideo | refreshChannel | refreshChannelVideos.</param>
public sealed record RunLock(SyncMode Task, SyncTrigger Trigger, DateTime Since)
{
    public static explicit operator RunLock(SyncLockInfo holder)
    {
        ArgumentNullException.ThrowIfNull(holder);
        return new RunLock(holder.Mode, holder.Trigger, holder.AcquiredAt.UtcDateTime);
    }
}
