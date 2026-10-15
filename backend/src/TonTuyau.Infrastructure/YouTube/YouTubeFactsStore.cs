using Microsoft.EntityFrameworkCore;
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
                row = new YouTubeVideoRow { VideoId = facts.VideoId };
                db.YouTubeVideos.Add(row);
            }

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
            row.ThumbnailPath = refreshed?.ThumbnailPath;
            await db
                .SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);
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
