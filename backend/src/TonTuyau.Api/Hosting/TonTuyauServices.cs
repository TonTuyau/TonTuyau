using TonTuyau.Application.Configuration;
using TonTuyau.Application.Sync;
using TonTuyau.Application.Validation;
using TonTuyau.Application.YouTube;
using TonTuyau.Infrastructure;
using TonTuyau.TubeArchivist;
using TonTuyau.YouTube;

namespace TonTuyau.Api.Hosting;

/// <summary>Everything the backend needs, shared by the web host and the CLI host.</summary>
internal static class TonTuyauServices
{
    public static void AddTonTuyauConfiguration(this IConfigurationBuilder configuration) =>
        configuration.AddEnvironmentVariables(prefix: ConfigKeys.EnvironmentPrefix);

    public static IServiceCollection AddTonTuyau(this IServiceCollection services)
    {
        services.AddSingleton(sp => TonTuyauOptionsReader.Read(sp.GetRequiredService<IConfiguration>()));
        services.AddSingleton<IValidator<TonTuyauOptions>, TonTuyauOptionsValidator>();
        services.AddTubeArchivistGateway();
        services.AddYouTubeClient();
        services.AddSearchStore();
        services.AddSingleton<SyncService>();
        services.AddSingleton<YouTubeRefreshService>();
        services.AddSingleton(sp => new SyncSchedule(sp.GetRequiredService<TonTuyauOptions>().Sync));
        return services;
    }

    /// <summary>Broken configuration stops the process before it does anything.</summary>
    public static void ValidateTonTuyauOptions(this IServiceProvider services) =>
        services
            .GetRequiredService<IValidator<TonTuyauOptions>>()
            .Validate(services.GetRequiredService<TonTuyauOptions>());
}
