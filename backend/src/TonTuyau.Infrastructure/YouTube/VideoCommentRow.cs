using TonTuyau.Application.YouTube;

namespace TonTuyau.Infrastructure.YouTube;

/// <summary>
/// A row of <c>video_comments</c> (1.5): a comment of a video as TonTuyau last read it, answers included. A refresh
/// replaces a video's comments as a whole.
/// </summary>
public sealed class VideoCommentRow
{
    public required string VideoId { get; set; }

    public required string CommentId { get; set; }

    /// <summary>Where YouTube showed it ("Top comments" first), from 0.</summary>
    public int Position { get; set; }

    /// <summary>The comment it answers; null for a comment on the video itself.</summary>
    public string? ParentId { get; set; }

    public required string Text { get; set; }

    public required string Author { get; set; }

    public string? AuthorId { get; set; }

    public string? AuthorThumbnail { get; set; }

    public DateTimeOffset? Published { get; set; }

    public long? Likes { get; set; }

    public bool Pinned { get; set; }

    public bool ByUploader { get; set; }

    public bool Hearted { get; set; }

    public static VideoCommentRow Of(string videoId, int position, YouTubeComment comment)
    {
        ArgumentNullException.ThrowIfNull(comment);
        return new VideoCommentRow
        {
            VideoId = videoId,
            CommentId = comment.Id,
            Position = position,
            ParentId = comment.ParentId,
            Text = comment.Text,
            Author = comment.Author,
            AuthorId = comment.AuthorId,
            AuthorThumbnail = comment.AuthorThumbnail,
            Published = comment.Published,
            Likes = comment.Likes,
            Pinned = comment.Pinned,
            ByUploader = comment.ByUploader,
            Hearted = comment.Hearted,
        };
    }
}
