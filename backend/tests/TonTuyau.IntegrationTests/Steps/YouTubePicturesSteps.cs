using Microsoft.Extensions.DependencyInjection;
using TonTuyau.IntegrationTests.Support;
using YouTubeServices = TonTuyau.YouTube.ServiceCollectionExtensions;

namespace TonTuyau.IntegrationTests.Steps;

/// <summary>YouTube's pictures (1.5), from a fake of its picture hosts (<see cref="FakePictures"/>).</summary>
[Binding]
public sealed class YouTubePicturesSteps(ScenarioState state) : IAsyncDisposable
{
    private FakePictures? _pictures;

    /// <summary>Before the backend starts: its pictures client is pointed at the fake.</summary>
    [Given("YouTube's pictures can be downloaded")]
    public async Task Start()
    {
        _pictures = await FakePictures.StartAsync();
        Uri fake = new(_pictures.Url);
        state.ServiceOverrides.Add(services => services
            .AddHttpClient(YouTubeServices.ImagesClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new OutsideRedirect("YouTube's pictures", fake)));
    }

    [Then("YouTube's pictures were asked for {string}")]
    public void Asked(string paths) =>
        Assert.Equal(paths.Split(", "), _pictures!.Requests.Order(StringComparer.Ordinal).ToArray());

    public async ValueTask DisposeAsync()
    {
        if (_pictures is not null)
        {
            await _pictures.DisposeAsync();
        }
    }
}
