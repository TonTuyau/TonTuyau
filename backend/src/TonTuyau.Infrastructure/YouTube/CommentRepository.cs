using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.Comments;
using TonTuyau.Application.YouTube;
using TonTuyau.Infrastructure.Accounts;
using TonTuyau.Infrastructure.Library;

namespace TonTuyau.Infrastructure.YouTube;

/// <summary>The comments' database calls (EF Core, PostgreSQL): a singleton, a database context per call.</summary>
internal sealed class CommentRepository(IServiceScopeFactory scopes) : ICommentRepository
{
    public async Task<CommentsRead?> GetReadAsync(string videoId, CancellationToken cancellationToken)
    {
        AsyncServiceScope scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            TonTuyauDbContext db = scope.ServiceProvider.GetRequiredService<TonTuyauDbContext>();
            VideoRow? video = await db.Videos
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == videoId, cancellationToken)
                .ConfigureAwait(false);
            if (video?.CommentsReadAt is null)
            {
                return null;
            }

            long kept = await db.VideoComments
                .LongCountAsync(c => c.VideoId == videoId, cancellationToken)
                .ConfigureAwait(false);
            return new CommentsRead(video.CommentsReadAt.Value, kept, video.CommentCount);
        }
    }

    public async Task<(IReadOnlyList<CommentThread> Threads, long Total)> GetThreadsAsync(
        string videoId,
        CommentSort sort,
        int offset,
        int size,
        CancellationToken cancellationToken)
    {
        AsyncServiceScope scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            TonTuyauDbContext db = scope.ServiceProvider.GetRequiredService<TonTuyauDbContext>();
            IQueryable<VideoCommentRow> parents = db.VideoComments
                .AsNoTracking()
                .Where(c => c.VideoId == videoId && c.ParentId == null);
            long total = await parents
                .LongCountAsync(cancellationToken)
                .ConfigureAwait(false);
            IQueryable<VideoCommentRow> ordered = sort == CommentSort.Newest
                ? parents
                    .OrderByDescending(c => c.Published.HasValue)
                    .ThenByDescending(c => c.Published)
                    .ThenBy(c => c.Position)
                : parents.OrderBy(c => c.Position);
            List<VideoCommentRow> page = await ordered
                .Skip(offset)
                .Take(size)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            List<string> ids = [.. page.Select(c => c.CommentId)];
            List<VideoCommentRow> replies = await db.VideoComments
                .AsNoTracking()
                .Where(c => c.VideoId == videoId && c.ParentId != null && ids.Contains(c.ParentId))
                .OrderBy(c => c.Position)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            ILookup<string, VideoCommentRow> byParent = replies.ToLookup(r => r.ParentId!, StringComparer.Ordinal);
            CommentThread[] threads =
            [
                .. page.Select(c => new CommentThread(
                    Comment(c),
                    [.. byParent[c.CommentId].Select(Comment)])),
            ];
            return (threads, total);
        }
    }

    private static YouTubeComment Comment(VideoCommentRow row) => new(
        row.CommentId,
        row.ParentId,
        row.Text,
        row.Author,
        row.AuthorId,
        row.AuthorThumbnail,
        row.Published,
        row.Likes,
        row.Pinned,
        row.ByUploader,
        row.Hearted);
}
