namespace TonTuyau.Infrastructure.Runs;

/// <summary>A row of <c>pending_art</c> (2.0): a picture of TubeArchivist's cache that failed to copy, retried later.</summary>
public sealed class PendingArtRow
{
    public required string Path { get; set; }
}
