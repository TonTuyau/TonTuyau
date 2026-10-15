using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.YouTube;

namespace TonTuyau.YouTube;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IYouTubeClient"/>: yt-dlp, as <c>TONTUYAU_YTDLP_PATH</c> names it (1.5).</summary>
    public static IServiceCollection AddYouTubeClient(this IServiceCollection services)
    {
        services.AddSingleton<IYouTubeClient, YouTubeClient>();
        return services;
    }
}
