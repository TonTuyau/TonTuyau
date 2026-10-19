namespace TonTuyau.Application.Store;

public enum StoreIndex
{
    Videos,
    Channels,
}

/// <param name="ReadIndex">Concrete index the read alias points to (what users browse).</param>
/// <param name="WriteIndex">
/// Concrete index the write alias points to (where the writes go). Differs from ReadIndex during a rebuild.
/// </param>
/// <param name="DefinitionUpToDate">False when the index was created from an older definition (mappings/analyzers): run a rebuild.</param>
public sealed record IndexStatus(
    StoreIndex Index,
    string ReadIndex,
    string WriteIndex,
    bool DefinitionUpToDate,
    long Documents);

/// <summary>A rebuild in progress: new generations receive the writes, reads stay on the old ones.</summary>
public sealed record IndexRebuild(
    IReadOnlyDictionary<StoreIndex, string> OldIndices,
    IReadOnlyDictionary<StoreIndex, string> NewIndices);

/// <summary>Creates TonTuyau's indices and runs rebuilds by swapping aliases, so browsing never sees a half-built index.</summary>
public interface IIndexManager
{
    /// <summary>Creates missing indices and aliases. Existing ones are left untouched.</summary>
    Task EnsureAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<IndexStatus>> GetStatusAsync(CancellationToken cancellationToken);

    /// <summary>Creates the next generation of every index and points the write aliases at it.</summary>
    Task<IndexRebuild> BeginRebuildAsync(CancellationToken cancellationToken);

    /// <summary>Switches every read alias to the new generation at once, then deletes the old generation.</summary>
    Task CompleteRebuildAsync(IndexRebuild rebuild, CancellationToken cancellationToken);

    /// <summary>Points the write aliases back to the old generation and deletes the new one.</summary>
    Task AbortRebuildAsync(IndexRebuild rebuild, CancellationToken cancellationToken);

    /// <summary>Makes recent writes visible to reads (tests and end-of-run).</summary>
    Task RefreshAsync(CancellationToken cancellationToken);
}
