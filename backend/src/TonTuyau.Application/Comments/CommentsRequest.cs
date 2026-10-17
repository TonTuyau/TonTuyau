namespace TonTuyau.Application.Comments;

/// <param name="Sort"><c>top</c> (the default) or <c>newest</c>.</param>
public sealed record CommentsRequest(string VideoId, string? Sort, string? Cursor, int? Size);
