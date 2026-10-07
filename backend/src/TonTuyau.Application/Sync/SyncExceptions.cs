namespace TonTuyau.Application.Sync;

/// <summary>Another sync holds the lock.</summary>
public sealed class SyncBusyException(SyncLockInfo? holder) : Exception(Describe(holder))
{
    public SyncLockInfo? Holder { get; } = holder;

    private static string Describe(SyncLockInfo? holder) =>
        holder is null
            ? "A sync is already running."
            : $"A sync is already running: {holder.Mode} by {holder.Trigger} ({holder.Owner}), "
                + $"started {holder.AcquiredAt:u}.";
}

/// <summary>A sync command that doesn't make sense in the current state (e.g. applying an outdated deletion list).</summary>
public sealed class SyncRefusedException(string message) : Exception(message);

/// <summary>Too many TubeArchivist documents fail validation: its format probably changed. The run stops.</summary>
public sealed class TubeArchivistFormatChangedException(int read, int rejected)
    : Exception(
        $"{rejected} of {read} TubeArchivist documents failed validation: its format may have changed. The run stopped.");
