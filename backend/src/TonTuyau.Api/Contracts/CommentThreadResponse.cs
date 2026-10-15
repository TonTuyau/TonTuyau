using TonTuyau.Application.Comments;

namespace TonTuyau.Api.Contracts;

/// <summary>A comment on the video and its answers read with it, in YouTube's order (contract v1.13).</summary>
public sealed record CommentThreadResponse(CommentResponse Comment, IReadOnlyList<CommentResponse> Replies)
{
    public static explicit operator CommentThreadResponse(CommentThread thread)
    {
        ArgumentNullException.ThrowIfNull(thread);
        return new CommentThreadResponse(
            (CommentResponse)thread.Comment,
            [.. thread.Replies.Select(r => (CommentResponse)r)]);
    }
}
