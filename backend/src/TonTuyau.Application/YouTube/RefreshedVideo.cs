namespace TonTuyau.Application.YouTube;

/// <summary>A video's metadata as TonTuyau last read it on YouTube (1.5): it wins over TubeArchivist's.</summary>
/// <param name="ThumbnailPath">The thumbnail TonTuyau stored in its art volume; null: TubeArchivist's stays.</param>
public sealed record RefreshedVideo(
    string Title,
    string Description,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Categories,
    long Views,
    long Likes,
    string? ThumbnailPath);
