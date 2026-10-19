using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.YouTube;
using TonTuyau.Infrastructure.Accounts;
using TonTuyau.Infrastructure.Library;

namespace TonTuyau.Infrastructure.YouTube;

/// <summary>
/// Writes what a refresh learned (a singleton like the refresh, a database context per call): since 2.0, the video's or
/// channel's row in the library, its state and, when read, its metadata in the columns the import filled. A video or
/// channel that left the library meanwhile has nothing recorded.
/// </summary>
internal sealed class YouTubeFactsStore(IServiceScopeFactory scopes) : IYouTubeFactsStore
{
    public async Task SaveVideoAsync(VideoFacts facts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(facts);
        AsyncServiceScope scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            TonTuyauDbContext db = scope.ServiceProvider.GetRequiredService<TonTuyauDbContext>();
            VideoRow? row = await db.Videos
                .FirstOrDefaultAsync(v => v.Id == facts.VideoId, cancellationToken)
                .ConfigureAwait(false);
            if (row is null)
            {
                return;
            }

            Dictionary<string, CategoryRow> categories = await LibraryCategories
                .FindOrAddAsync(db, [facts.Refreshed?.Categories ?? []], cancellationToken)
                .ConfigureAwait(false);
            LibraryRows.WriteFacts(row, facts, LibraryCategories.Of(facts.Refreshed?.Categories ?? [], categories));
            await db
                .SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task SaveCommentsAsync(
        string videoId,
        IReadOnlyList<YouTubeComment> comments,
        DateTimeOffset readAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comments);
        AsyncServiceScope scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            TonTuyauDbContext db = scope.ServiceProvider.GetRequiredService<TonTuyauDbContext>();
            VideoRow? row = await db.Videos
                .FirstOrDefaultAsync(v => v.Id == videoId, cancellationToken)
                .ConfigureAwait(false);
            if (row is null)
            {
                return;
            }

            IDbContextTransaction transaction = await db.Database
                .BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                await db.VideoComments
                    .Where(c => c.VideoId == videoId)
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);
                // yt-dlp may list a comment twice (a pinned one, again in its place): the first one is kept.
                db.VideoComments.AddRange(comments
                    .DistinctBy(c => c.Id, StringComparer.Ordinal)
                    .Select((c, position) => VideoCommentRow.Of(videoId, position, c)));
                row.CommentsReadAt = readAt;
                await db
                    .SaveChangesAsync(cancellationToken)
                    .ConfigureAwait(false);
                await transaction
                    .CommitAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    public async Task SaveChannelAsync(ChannelFacts facts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(facts);
        AsyncServiceScope scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            TonTuyauDbContext db = scope.ServiceProvider.GetRequiredService<TonTuyauDbContext>();
            ChannelRow? row = await db.Channels
                .FirstOrDefaultAsync(c => c.Id == facts.ChannelId, cancellationToken)
                .ConfigureAwait(false);
            if (row is null)
            {
                return;
            }

            LibraryRows.WriteFacts(row, facts);
            await db
                .SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
