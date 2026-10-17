using TonTuyau.Application.YouTube;

namespace TonTuyau.Application.Comments;

/// <summary>A comment on the video and its answers (those read with it), in YouTube's order.</summary>
public sealed record CommentThread(YouTubeComment Comment, IReadOnlyList<YouTubeComment> Replies);
