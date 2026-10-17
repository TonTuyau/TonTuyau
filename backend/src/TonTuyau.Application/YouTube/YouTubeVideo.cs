namespace TonTuyau.Application.YouTube;

/// <summary>A video as YouTube describes it now (1.5), read through yt-dlp.</summary>
/// <param name="Published">When it was published (to the second when YouTube says, else the day at midnight UTC).</param>
/// <param name="LiveStatus">yt-dlp's <c>live_status</c>: <c>not_live</c>, <c>was_live</c>, <c>is_live</c>, <c>is_upcoming</c>…</param>
/// <param name="CommentCount">How many comments YouTube counts, asked for them or not.</param>
/// <param name="Comments">
/// The comments read, when asked for (<see cref="YouTubeCommentsRequest"/>); null otherwise, or when yt-dlp couldn't
/// read them.
/// </param>
/// <param name="Chapters">Its chapters, in order; none when YouTube marks none.</param>
/// <param name="Availability">yt-dlp's <c>availability</c>: <c>public</c>, <c>unlisted</c>, <c>private</c>…; null when it doesn't say.</param>
public sealed record YouTubeVideo(
    string Id,
    string Title,
    string Description,
    string ChannelId,
    string ChannelName,
    DateTimeOffset? Published,
    int? DurationSeconds,
    long? Views,
    long? Likes,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Categories,
    IReadOnlyList<YouTubeThumbnail> Thumbnails,
    string? LiveStatus,
    long? CommentCount,
    IReadOnlyList<YouTubeComment>? Comments,
    IReadOnlyList<YouTubeChapter> Chapters,
    string? Availability);
