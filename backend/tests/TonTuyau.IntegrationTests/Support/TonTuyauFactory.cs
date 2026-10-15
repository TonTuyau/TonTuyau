using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using TonTuyau.TubeArchivist;
using YouTubeServices = TonTuyau.YouTube.ServiceCollectionExtensions;

namespace TonTuyau.IntegrationTests.Support;

/// <summary>Runs the real backend in-process with the settings (and service overrides) given by the scenario.</summary>
public sealed class TonTuyauFactory(
    IReadOnlyDictionary<string, string?> settings,
    IReadOnlyList<Action<IServiceCollection>> overrides) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(settings));
        builder.ConfigureTestServices(services =>
        {
            // Same retry policy as production, without the waiting.
            services.Configure<HttpStandardResilienceOptions>(
                $"{ServiceCollectionExtensions.HttpClientName}-standard",
                o =>
                {
                    o.Retry.Delay = TimeSpan.FromMilliseconds(1);
                    o.Retry.UseJitter = false;
                });
            // Never the real GitHub nor YouTube: a scenario that starts a fake points the client there (after this).
            services
                .AddHttpClient(YouTubeServices.GitHubClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new OutsideRedirect("GitHub", fake: null));
            services
                .AddHttpClient(YouTubeServices.ImagesClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new OutsideRedirect("YouTube's pictures", fake: null));
            foreach (Action<IServiceCollection> apply in overrides)
            {
                apply(services);
            }
        });
    }
}
