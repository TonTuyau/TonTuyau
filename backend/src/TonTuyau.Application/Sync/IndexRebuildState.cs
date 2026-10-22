namespace TonTuyau.Application.Sync;

/// <summary>Whether the indices need the rebuild a new index definition calls for (QA10 M5-10).</summary>
public enum IndexRebuildStatus
{
    /// <summary>Not checked yet (Elasticsearch not reached).</summary>
    Unknown,

    UpToDate,

    /// <summary>The definitions changed: the startup auto-rebuild, or the next import, rebuilds.</summary>
    Pending,

    Running,
}

/// <summary>This process's view of <see cref="IndexRebuildStatus"/>, for the readiness check and the logs.</summary>
public sealed class IndexRebuildState
{
    private int _status;

    public IndexRebuildStatus Status
    {
        get => (IndexRebuildStatus)Volatile.Read(ref _status);
        set => Volatile.Write(ref _status, (int)value);
    }
}
