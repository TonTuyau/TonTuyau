using TonTuyau.Application.YouTube;

namespace TonTuyau.Api.Contracts;

/// <summary>A comment, as TonTuyau read it on YouTube (contract v1.13). Its author's picture isn't given.</summary>
/// <param name="ByUploader">Written by the video's own channel.</param>
/// <param name="Hearted">Marked with a heart by the video's channel.</param>
public sealed record CommentResponse(
    string Id,
    string Text,
    string Author,
    DateTime? Published,
    long? Likes,
    bool Pinned,
    bool ByUploader,
    bool Hearted)
{
    public static explicit operator CommentResponse(YouTubeComment comment)
    {
        ArgumentNullException.ThrowIfNull(comment);
        return new CommentResponse(
            comment.Id,
            comment.Text,
            comment.Author,
            comment.Published?.UtcDateTime,
            comment.Likes,
            comment.Pinned,
            comment.ByUploader,
            comment.Hearted);
    }
}
