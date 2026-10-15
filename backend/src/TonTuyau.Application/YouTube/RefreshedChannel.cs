namespace TonTuyau.Application.YouTube;

/// <summary>A channel's metadata as TonTuyau last read it on YouTube (1.5): it wins over TubeArchivist's.</summary>
/// <param name="ThumbPath">The pictures TonTuyau stored in its art volume; null: TubeArchivist's stay.</param>
public sealed record RefreshedChannel(
    string Name,
    string Description,
    long Subscribers,
    IReadOnlyList<string> Tags,
    string? ThumbPath,
    string? BannerPath);
