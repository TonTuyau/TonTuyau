namespace TonTuyau.Application.Comments;

/// <summary>When TonTuyau last read a video's comments, and how many it keeps (answers included).</summary>
/// <param name="YouTubeCount">How many comments YouTube counts; null when it didn't say.</param>
public sealed record CommentsRead(DateTimeOffset ReadAt, long Kept, long? YouTubeCount);
