namespace TonTuyau.Application.Comments;

/// <summary>The comments TonTuyau read on YouTube (1.5, PostgreSQL <c>video_comments</c>).</summary>
public interface ICommentRepository
{
    /// <returns>Null when TonTuyau never read the video's comments.</returns>
    Task<CommentsRead?> GetReadAsync(string videoId, CancellationToken cancellationToken);

    /// <summary>A page of the video's threads (its comments, each with its answers).</summary>
    /// <returns>The threads, and how many there are in all.</returns>
    Task<(IReadOnlyList<CommentThread> Threads, long Total)> GetThreadsAsync(
        string videoId,
        CommentSort sort,
        int offset,
        int size,
        CancellationToken cancellationToken);
}
