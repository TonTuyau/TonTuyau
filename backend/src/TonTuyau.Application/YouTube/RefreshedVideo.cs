namespace TonTuyau.Application.YouTube;

/// <summary>A video's metadata as TonTuyau last read it on YouTube (1.5): it wins over TubeArchivist's.</summary>
/// <param name="ChannelName">The channel's name as YouTube shows it with the video.</param>
/// <param name="CommentCount">How many comments YouTube counts; null when it doesn't say.</param>
/// <param name="Chapters">Its chapters, in order (the player shows them); none when YouTube marks none.</param>
/// <param name="ThumbnailPath">The thumbnail TonTuyau stored in its art volume; null: TubeArchivist's stays.</param>
public sealed record RefreshedVideo(
    string Title,
    string ChannelName,
    string Description,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Categories,
    long Views,
    long Likes,
    long? CommentCount,
    IReadOnlyList<YouTubeChapter> Chapters,
    string? ThumbnailPath);
