using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.YouTube;
using TonTuyau.Infrastructure.Accounts;

namespace TonTuyau.Infrastructure.YouTube;

/// <summary>
/// TonTuyau's own facts for the sync's writes (one query per page): a singleton like the sync, a database context per
/// call.
/// </summary>
internal sealed class YouTubeFactsSource(IServiceScopeFactory scopes) : IYouTubeFactsSource
{
    public async Task<IReadOnlyDictionary<string, VideoFacts>> GetVideosAsync(
        IReadOnlyCollection<string> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<string, VideoFacts>();
        }

        AsyncServiceScope scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            List<YouTubeVideoRow> rows = await scope.ServiceProvider
                .GetRequiredService<TonTuyauDbContext>()
                .YouTubeVideos
                .AsNoTracking()
                .Where(v => ids.Contains(v.VideoId))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            return rows.ToDictionary(r => r.VideoId, r => (VideoFacts)r, StringComparer.Ordinal);
        }
    }

    public async Task<IReadOnlyDictionary<string, ChannelFacts>> GetChannelsAsync(
        IReadOnlyCollection<string> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<string, ChannelFacts>();
        }

        AsyncServiceScope scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            List<YouTubeChannelRow> rows = await scope.ServiceProvider
                .GetRequiredService<TonTuyauDbContext>()
                .YouTubeChannels
                .AsNoTracking()
                .Where(c => ids.Contains(c.ChannelId))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            return rows.ToDictionary(r => r.ChannelId, r => (ChannelFacts)r, StringComparer.Ordinal);
        }
    }
}
