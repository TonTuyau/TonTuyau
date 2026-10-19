namespace TonTuyau.Application.YouTube;

/// <summary>
/// Records what a refresh learned (1.5) in the library's rows (2.0): the state, and when it read them, the metadata,
/// written in the same columns the import filled (what TonTuyau read last is what the row holds).
/// </summary>
public interface IYouTubeFactsStore
{
    Task SaveVideoAsync(VideoFacts facts, CancellationToken cancellationToken);

    Task SaveChannelAsync(ChannelFacts facts, CancellationToken cancellationToken);

    /// <summary>Replaces a video's comments (its facts saved first), and records when they were read.</summary>
    /// <param name="comments">In YouTube's order, answers included; none: the video has none.</param>
    Task SaveCommentsAsync(
        string videoId,
        IReadOnlyList<YouTubeComment> comments,
        DateTimeOffset readAt,
        CancellationToken cancellationToken);
}
