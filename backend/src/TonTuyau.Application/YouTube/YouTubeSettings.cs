namespace TonTuyau.Application.YouTube;

/// <summary>How TonTuyau reads YouTube (1.5), chosen on the admin's YouTube page.</summary>
/// <param name="ReadComments">
/// Whether a refresh reads each video's comments too (up to <see cref="YouTubeRefreshService.CommentsKept"/>): slower,
/// so off by default.
/// </param>
/// <param name="ChangedBy">The admin who last changed them; null while they're the defaults.</param>
public sealed record YouTubeSettings(bool ReadComments, string? ChangedBy, DateTimeOffset? ChangedAt)
{
    public static readonly YouTubeSettings Default = new(ReadComments: false, null, null);
}
