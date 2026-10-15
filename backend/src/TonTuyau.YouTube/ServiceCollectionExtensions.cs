using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.Configuration;
using TonTuyau.Application.YouTube;
using TonTuyau.YouTube.Releases;

namespace TonTuyau.YouTube;

public static class ServiceCollectionExtensions
{
    public const string GitHubClientName = "yt-dlp-releases";
    public const string UserAgent = "TonTuyau (+https://github.com/Eclegv/TonTuyau)";

    /// <summary>
    /// Registers <see cref="IYouTubeClient"/> (1.5): yt-dlp, the image's copy (<c>TONTUYAU_YTDLP_PATH</c>) until another
    /// version is chosen; yt-dlp's releases on GitHub, and the versions installed in the tools folder.
    /// </summary>
    public static IServiceCollection AddYouTubeClient(this IServiceCollection services)
    {
        services.AddSingleton(sp => new YtdlpLocator(sp.GetRequiredService<TonTuyauOptions>().YouTube.YtdlpPath));
        services.AddSingleton<YtdlpUpdateSignal>();
        services.AddSingleton<IYouTubeClient, YouTubeClient>();
        // GitHub asks every request for a User-Agent.
        services.AddHttpClient(
            GitHubClientName,
            client => client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent));
        services.AddSingleton<IYtdlpReleases>(sp => new GitHubYtdlpReleases(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(GitHubClientName),
            sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IYtdlpInstaller>(sp => new YtdlpInstaller(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(GitHubClientName),
            sp.GetRequiredService<TonTuyauOptions>()));
        return services;
    }
}
