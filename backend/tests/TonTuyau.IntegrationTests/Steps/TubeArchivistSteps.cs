using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.Configuration;
using TonTuyau.Application.TubeArchivist;
using TonTuyau.IntegrationTests.Support;
using TonTuyau.TubeArchivist;
using TonTuyau.TubeArchivist.Http;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace TonTuyau.IntegrationTests.Steps;

[Binding]
public sealed class TubeArchivistSteps(ScenarioState state) : IDisposable
{
    private WireMockServer? _fake;
    private TubeArchivistInfo? _info;

    private WireMockServer Fake
        => _fake ?? throw new InvalidOperationException("No fake TubeArchivist in this scenario.");

    [Given("a fake TubeArchivist")]
    public void GivenAFakeTubeArchivist()
    {
        _fake = WireMockServer.Start();
        Fake.Given(Request.Create().WithPath("/api/ping/").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithBodyAsJson(new { response = "pong", user = 1, version = "v0.5.12" }));
        // Anything else is accepted too, so a request that slipped through would be visible in the log.
        Fake.Given(Request.Create().UsingAnyMethod()).AtPriority(100)
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("{}"));
    }

    [Given("the backend is configured to use it")]
    public void GivenTheBackendUsesIt() =>
        state.Settings.Set(ConfigKeys.EnvironmentName(ConfigKeys.TaUrl), Fake.Url);

    [Given("the fake TubeArchivist answers {int} to every request")]
    public void GivenItAnswersEverything(int status)
    {
        Fake.Reset();
        Fake.Given(Request.Create().UsingAnyMethod())
            .RespondWith(Response.Create().WithStatusCode(status).WithBody("<h1>down</h1>"));
    }

    [Given("the fake TubeArchivist refuses the token")]
    public void GivenItRefusesTheToken()
    {
        Fake.Reset();
        Fake.Given(Request.Create().UsingAnyMethod())
            .RespondWith(Response.Create().WithStatusCode(403).WithBodyAsJson(new { detail = "Invalid token." }));
    }

    [When("TonTuyau's TubeArchivist client is asked to send a {word} request")]
    public async Task WhenTheClientSends(string method)
    {
        HttpClient client = state.Backend.Services.GetRequiredService<IHttpClientFactory>()
            .CreateClient(ServiceCollectionExtensions.HttpClientName);
        using HttpRequestMessage request = new(new HttpMethod(method), "api/video/abcdefghijk/")
        {
            Content = new StringContent("{}")
        };
        try
        {
            using HttpResponseMessage response = await client.SendAsync(request);
        }
#pragma warning disable CA1031 // The scenario asserts on whatever the client throws.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            state.Error = ex;
        }
    }

    [When("TonTuyau pings TubeArchivist")]
    public async Task WhenTonTuyauPings()
    {
        try
        {
            _info = await state.Backend.Services.GetRequiredService<ITubeArchivistGateway>()
                .PingAsync(CancellationToken.None);
        }
        catch (TubeArchivistException ex)
        {
            state.Error = ex;
        }
    }

    [Then("the request is refused as a read-only violation")]
    public void ThenRefused() => Assert.IsType<ReadOnlyViolationException>(state.Error);

    [Then("the fake TubeArchivist received no request")]
    public void ThenNoRequest() => Assert.Empty(Fake.LogEntries);

    [Then("TubeArchivist's version is {string}")]
    public void ThenVersionIs(string version) => Assert.Equal(version, _info?.Version);

    [Then("the fake TubeArchivist received {int} GET request(s) to {string}")]
    public void ThenReceived(int count, string path)
    {
        Assert.Equal(count, Fake.LogEntries.Count);
        Assert.All(
            Fake.LogEntries,
            e => Assert.Equal(("GET", path), (e.RequestMessage!.Method, e.RequestMessage.Path)));
    }

    [Then("every request carried the API token")]
    public void ThenTokenSent()
    {
        Assert.NotEmpty(Fake.LogEntries);
        Assert.All(
            Fake.LogEntries,
            e => Assert.Equal($"Token {TestSettings.Token}", e.RequestMessage!.Headers!["Authorization"].Single()));
    }

    [Then("every request the fake TubeArchivist received was a GET")]
    public void ThenOnlyGets()
    {
        Assert.NotEmpty(Fake.LogEntries);
        Assert.All(Fake.LogEntries, e => Assert.Equal("GET", e.RequestMessage!.Method));
    }

    [Then("TonTuyau reports that TubeArchivist is failing")]
    public void ThenFailing() => Assert.IsType<TubeArchivistServerException>(state.Error);

    [Then("TonTuyau reports an authentication failure")]
    public void ThenAuthFailure() => Assert.IsType<TubeArchivistAuthException>(state.Error);

    public void Dispose() => _fake?.Dispose();
}
