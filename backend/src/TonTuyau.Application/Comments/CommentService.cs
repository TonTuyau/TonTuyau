using TonTuyau.Application.Browse;
using TonTuyau.Application.Paging;
using TonTuyau.Application.Store;
using TonTuyau.Application.Validation;

namespace TonTuyau.Application.Comments;

/// <summary>
/// A video's comments, as TonTuyau read them on YouTube (1.5): a page of threads (a comment and its answers), in
/// YouTube's order or the newest first. They don't change between two refreshes, so a page is where it starts.
/// </summary>
public sealed class CommentService(ICommentRepository comments, IVideoRepository videos)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <exception cref="ValidationException">The request isn't valid.</exception>
    /// <exception cref="NotFoundException">No video with this id.</exception>
    public async Task<CommentsView> GetAsync(CommentsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidationErrors errors = new();
        RequestRules.VideoId(errors, "id", request.VideoId);
        RequestRules.OneOf(errors, "sort", request.Sort, ApiValues.CommentSorts);
        RequestRules.Size(errors, request.Size, MaxPageSize);
        errors.ThrowIfAny(RequestRules.Subject);
        CommentSort sort = request.Sort is null ? CommentSort.Top : ApiValues.CommentSorts[request.Sort];
        string fingerprint = CursorCodec.Fingerprint($"comments|{request.VideoId}|{sort}");
        int offset = OffsetOf(CursorCodec.Decode(request.Cursor, fingerprint));
        if (await videos.GetAsync(request.VideoId, cancellationToken).ConfigureAwait(false) is null)
        {
            throw new NotFoundException($"There's no video with the id '{request.VideoId}'.");
        }

        CommentsRead? read = await comments.GetReadAsync(request.VideoId, cancellationToken).ConfigureAwait(false);
        if (read is null)
        {
            return new CommentsView(null, new ResultPage<CommentThread>([], null, 0));
        }

        int size = request.Size ?? DefaultPageSize;
        (IReadOnlyList<CommentThread> threads, long total) = await comments
            .GetThreadsAsync(request.VideoId, sort, offset, size, cancellationToken)
            .ConfigureAwait(false);
        string? next = offset + threads.Count < total
            ? CursorCodec.Encode(fingerprint, new SortPosition([new IntegerSortValue(offset + threads.Count)]))
            : null;
        return new CommentsView(read, new ResultPage<CommentThread>(threads, next, total));
    }

    private static int OffsetOf(PageCursor? cursor) =>
        cursor?.After.Values is [IntegerSortValue { Value: >= 0 and <= int.MaxValue } offset]
            ? (int)offset.Value
            : cursor is null
                ? 0
                : throw CursorCodec.Invalid();
}
