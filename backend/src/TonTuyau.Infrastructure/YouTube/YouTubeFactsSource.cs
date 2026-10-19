using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.YouTube;
using TonTuyau.Infrastructure.Accounts;
using TonTuyau.Infrastructure.Library;

namespace TonTuyau.Infrastructure.YouTube;

/// <summary>
/// What TonTuyau knows of videos and channels on YouTube, read from the library's rows (2.0): a singleton like the
/// refresh, a database context per call.
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
            List<VideoRow> rows = await scope.ServiceProvider
                .GetRequiredService<TonTuyauDbContext>()
                .Videos
                .AsNoTracking()
                .Where(v => ids.Contains(v.Id))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            return rows.ToDictionary(r => r.Id, LibraryRows.FactsOf, StringComparer.Ordinal);
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
            List<ChannelRow> rows = await scope.ServiceProvider
                .GetRequiredService<TonTuyauDbContext>()
                .Channels
                .AsNoTracking()
                .Where(c => ids.Contains(c.Id))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            return rows.ToDictionary(r => r.Id, LibraryRows.FactsOf, StringComparer.Ordinal);
        }
    }
}
