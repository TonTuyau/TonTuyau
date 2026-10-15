namespace TonTuyau.Application.YouTube;

/// <summary>How TonTuyau reads YouTube (1.5), chosen on the admin's YouTube page.</summary>
/// <param name="Comments">Which comments a refresh reads with each video: none by default (reading them is slow).</param>
/// <param name="CommentsMax">
/// For <see cref="YouTubeCommentsChoice.Top"/>: how many, answers included (1 or more). Kept
/// with the other choices, for when Top is chosen again.
/// </param>
/// <param name="ChangedBy">The admin who last changed them; null while they're the defaults.</param>
public sealed record YouTubeSettings(
    YouTubeCommentsChoice Comments,
    int CommentsMax,
    string? ChangedBy,
    DateTimeOffset? ChangedAt)
{
    public const int DefaultCommentsMax = 500;

    public static readonly YouTubeSettings Default = new(YouTubeCommentsChoice.Off, DefaultCommentsMax, null, null);

    /// <summary>What to ask yt-dlp for; null: no comments.</summary>
    public YouTubeCommentsRequest? CommentsRequest => Comments switch
    {
        YouTubeCommentsChoice.Top => new YouTubeCommentsRequest(CommentsMax),
        YouTubeCommentsChoice.All => new YouTubeCommentsRequest(null),
        _ => null,
    };
}
