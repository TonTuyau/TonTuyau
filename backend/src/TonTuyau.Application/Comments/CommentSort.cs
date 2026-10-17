namespace TonTuyau.Application.Comments;

/// <summary>How a video's comments are listed (1.5): YouTube's order, or the newest first.</summary>
public enum CommentSort
{
    /// <summary>YouTube's "Top comments" order, as read.</summary>
    Top,

    /// <summary>The newest first (the same comments, by date).</summary>
    Newest,
}
