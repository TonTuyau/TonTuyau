using TonTuyau.Application.Sync;

namespace TonTuyau.Infrastructure.Runs;

/// <summary>
/// The only row of <c>sync_lock</c> (2.0): who holds the lock that lets one run at a time, until when. Taken by
/// inserting it, renewed and released by its owner only; an expired one may be taken over.
/// </summary>
public sealed class SyncLockRow
{
    public const int OnlyId = 1;

    public int Id { get; set; } = OnlyId;

    public required string Owner { get; set; }

    public SyncMode Mode { get; set; }

    public SyncTrigger Trigger { get; set; }

    public DateTimeOffset AcquiredAt { get; set; }

    public DateTimeOffset LeaseUntil { get; set; }
}
