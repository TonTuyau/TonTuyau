using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.YouTube;
using TonTuyau.Infrastructure.Accounts;

namespace TonTuyau.Infrastructure.YouTube;

/// <summary>Writes TonTuyau's own facts (a singleton like the refresh, a database context per call).</summary>
internal sealed class YouTubeFactsStore(IServiceScopeFactory scopes) : IYouTubeFactsStore
{
    public async Task SaveVideoAsync(VideoFacts facts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(facts);
        AsyncServiceScope scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            TonTuyauDbContext db = scope.ServiceProvider.GetRequiredService<TonTuyauDbContext>();
            YouTubeVideoRow? row = await db.YouTubeVideos
                .FirstOrDefaultAsync(v => v.VideoId == facts.VideoId, cancellationToken)
                .ConfigureAwait(false);
            if (row is null)
            {
                row = new YouTubeVideoRow { VideoId = facts.VideoId, ChannelId = facts.ChannelId };
                db.YouTubeVideos.Add(row);
            }

            // Its channel has a row before it (the foreign key): "not checked" until TonTuyau reads the channel.
            bool channelKnown = await db.YouTubeChannels
                .AnyAsync(c => c.ChannelId == facts.ChannelId, cancellationToken)
                .ConfigureAwait(false);
            if (!channelKnown)
            {
                db.YouTubeChannels.Add(new YouTubeChannelRow
                {
                    ChannelId = facts.ChannelId,
                    State = YouTubeState.NotChecked,
                });
            }

            row.ChannelId = facts.ChannelId;

            row.State = facts.Status.State;
            row.CheckedAt = facts.Status.CheckedAt;
            row.Reason = facts.Status.Reason;
            row.RefreshedAt = facts.Status.RefreshedAt;
            RefreshedVideo? refreshed = facts.Refreshed;
            row.Title = refreshed?.Title;
            row.ChannelName = refreshed?.ChannelName;
            row.Description = refreshed?.Description;
            row.Tags = [.. refreshed?.Tags ?? []];
            row.Categories = [.. refreshed?.Categories ?? []];
            row.Views = refreshed?.Views;
            row.Likes = refreshed?.Likes;
            row.CommentCount = refreshed?.CommentCount;
            row.Chapters =
            [
                .. (refreshed?.Chapters ?? []).Select(c => new VideoChapterRow
                {
                    Start = c.StartSeconds,
                    End = c.EndSeconds,
                    Title = c.Title,
                }),
            ];
            row.ThumbnailPath = refreshed?.ThumbnailPath;
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
            YouTubeVideoRow row = await db.YouTubeVideos
                .FirstAsync(v => v.VideoId == videoId, cancellationToken)
                .ConfigureAwait(false);
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
            YouTubeChannelRow? row = await db.YouTubeChannels
                .FirstOrDefaultAsync(c => c.ChannelId == facts.ChannelId, cancellationToken)
                .ConfigureAwait(false);
            if (row is null)
            {
                row = new YouTubeChannelRow { ChannelId = facts.ChannelId };
                db.YouTubeChannels.Add(row);
            }

            row.State = facts.Status.State;
            row.CheckedAt = facts.Status.CheckedAt;
            row.Reason = facts.Status.Reason;
            row.RefreshedAt = facts.Status.RefreshedAt;
            RefreshedChannel? refreshed = facts.Refreshed;
            row.Name = refreshed?.Name;
            row.Description = refreshed?.Description;
            row.Subscribers = refreshed?.Subscribers;
            row.Tags = [.. refreshed?.Tags ?? []];
            row.ThumbPath = refreshed?.ThumbPath;
            row.BannerPath = refreshed?.BannerPath;
            await db
                .SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
