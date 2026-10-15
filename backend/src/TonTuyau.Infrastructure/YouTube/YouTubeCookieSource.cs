using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.YouTube;

namespace TonTuyau.Infrastructure.YouTube;

/// <summary>
/// The cookie's content, read from the database for each yt-dlp run (one small read; a new cookie is used at once).
/// A singleton: the YouTube client is one, the database context is per scope.
/// </summary>
internal sealed class YouTubeCookieSource(IServiceScopeFactory scopes) : IYouTubeCookieSource
{
    public async Task<string?> GetAsync(CancellationToken cancellationToken)
    {
        AsyncServiceScope scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            YouTubeCookie? cookie = await scope.ServiceProvider
                .GetRequiredService<IYouTubeCookieRepository>()
                .GetAsync(cancellationToken)
                .ConfigureAwait(false);
            return cookie?.Content;
        }
    }
}
