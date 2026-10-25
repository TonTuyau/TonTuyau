namespace TonTuyau.Application.Sync;

/// <summary>Another run holds the lock.</summary>
public sealed class SyncBusyException(SyncLockInfo? holder) : Exception(Describe(holder))
{
    public SyncLockInfo? Holder { get; } = holder;

    private static string Describe(SyncLockInfo? holder) =>
        holder is null
            ? "A run is already going on."
            : $"A run is already going on: {holder.Mode} by {holder.Trigger} ({holder.Owner}), "
                + $"started {holder.AcquiredAt:u}.";
}

/// <summary>A run that can't start in the current state (e.g. an import without TubeArchivist).</summary>
public sealed class SyncRefusedException(string message) : Exception(message);

/// <summary>
/// The videos folder's disk is under the threshold and the downloads are set to wait (2.2.0): a sync going on stops
/// there, on hold.
/// </summary>
public sealed class DiskSpaceLowException(string message) : Exception(message);
