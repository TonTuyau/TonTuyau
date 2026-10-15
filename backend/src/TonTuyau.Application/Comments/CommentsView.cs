using TonTuyau.Application.Paging;

namespace TonTuyau.Application.Comments;

/// <summary>A video's comments for its watch page (1.5).</summary>
/// <param name="Read">When they were read and how many are kept; null: never read (the page shows no comments).</param>
public sealed record CommentsView(CommentsRead? Read, ResultPage<CommentThread> Threads);
