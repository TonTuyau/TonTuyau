namespace TonTuyau.Application.YouTube;

/// <summary>Records what a refresh learned (1.5): TonTuyau's facts in PostgreSQL, each replaced as a whole.</summary>
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
