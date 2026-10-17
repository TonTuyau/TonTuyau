namespace TonTuyau.Application.YouTube;

/// <summary>A comment of a video, as YouTube shows it (1.5).</summary>
/// <param name="ParentId">The comment it answers; null for a comment on the video itself.</param>
/// <param name="AuthorId">The author's channel id.</param>
/// <param name="ByUploader">Written by the video's own channel.</param>
/// <param name="Hearted">Marked with a heart by the video's channel.</param>
public sealed record YouTubeComment(
    string Id,
    string? ParentId,
    string Text,
    string Author,
    string? AuthorId,
    string? AuthorThumbnail,
    DateTimeOffset? Published,
    long? Likes,
    bool Pinned,
    bool ByUploader,
    bool Hearted);
