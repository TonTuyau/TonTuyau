using TonTuyau.Application.Comments;

namespace TonTuyau.Api.Contracts;

/// <summary>A video's comments, a page of threads (contract v1.13, TonTuyau 1.5).</summary>
/// <param name="ReadAt">When TonTuyau read them on YouTube; null: never (then no comments are shown at all).</param>
/// <param name="YouTubeCount">How many comments YouTube counts; null when unknown.</param>
/// <param name="Kept">How many TonTuyau keeps, answers included.</param>
/// <param name="Total">How many threads (comments on the video itself) in all.</param>
public sealed record CommentsResponse(
    DateTime? ReadAt,
    long? YouTubeCount,
    long Kept,
    IReadOnlyList<CommentThreadResponse> Items,
    string? NextCursor,
    long Total)
{
    public static explicit operator CommentsResponse(CommentsView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new CommentsResponse(
            view.Read?.ReadAt.UtcDateTime,
            view.Read?.YouTubeCount,
            view.Read?.Kept ?? 0,
            [.. view.Threads.Items.Select(t => (CommentThreadResponse)t)],
            view.Threads.NextCursor,
            view.Threads.Total);
    }
}
