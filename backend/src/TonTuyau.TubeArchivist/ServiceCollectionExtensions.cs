using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using TonTuyau.Application.Configuration;
using TonTuyau.Application.TubeArchivist;
using TonTuyau.Application.Validation;
using TonTuyau.TubeArchivist.Dtos;
using TonTuyau.TubeArchivist.Http;
using TonTuyau.TubeArchivist.Validation;

namespace TonTuyau.TubeArchivist;

public static class ServiceCollectionExtensions
{
    public const string HttpClientName = "TubeArchivist";

    /// <summary>
    /// Registers <see cref="ITubeArchivistGateway"/>. Handler order, outermost first:
    /// read-only guard → token → resilience (retries stay behind the guard) → network.
    /// </summary>
    public static IServiceCollection AddTubeArchivistGateway(this IServiceCollection services)
    {
        services.AddSingleton<IValidator<VideoDto>, VideoDtoValidator>();
        services.AddSingleton<IValidator<ChannelDto>, ChannelDtoValidator>();
        services.AddTransient<ReadOnlyGuardHandler>();
        // TubeArchivist is optional (2.0): without its settings, the gateway exists (the import needs one) but
        // nothing calls it, the import being refused first.
        services.AddTransient(sp => new TokenAuthHandler(
            sp.GetRequiredService<TonTuyauOptions>().TubeArchivist?.Token ?? ""));

        services.AddHttpClient<ITubeArchivistGateway, TubeArchivistGateway>(
            HttpClientName,
            (sp, client) =>
            {
                TubeArchivistOptions? tubeArchivist = sp.GetRequiredService<TonTuyauOptions>().TubeArchivist;
                if (tubeArchivist is not null)
                {
                    client.BaseAddress = new Uri(tubeArchivist.BaseUrl.TrimEnd('/') + "/");
                }

                client.Timeout = Timeout.InfiniteTimeSpan; // Timeouts are handled by the resilience pipeline.
            })
            .AddHttpMessageHandler<ReadOnlyGuardHandler>()
            .AddHttpMessageHandler<TokenAuthHandler>()
            .RedactLoggedHeaders(["Authorization"])
            .AddStandardResilienceHandler(ConfigureResilience);

        return services;
    }

    private static void ConfigureResilience(HttpStandardResilienceOptions options)
    {
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.Delay = TimeSpan.FromSeconds(1);
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(2);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(1);
    }
}
