using Microsoft.Extensions.DependencyInjection;

namespace TonTuyau.IntegrationTests.Support;

/// <summary>Per-scenario state shared by all step classes (Reqnroll context injection).</summary>
public sealed class ScenarioState : IDisposable
{
    private TonTuyauFactory? _factory;

    public TestSettings Settings { get; } = new();

    /// <summary>Service replacements for this scenario (e.g. shorter lock leases), applied when the backend starts.</summary>
    public List<Action<IServiceCollection>> ServiceOverrides { get; } = [];

    /// <summary>The last exception a When step caught, for Then steps to inspect.</summary>
    public Exception? Error { get; set; }

    /// <summary>The last HTTP answer of the backend, its body already read (<see cref="ResponseBody"/>).</summary>
    public HttpResponseMessage? Response { get; private set; }

    public byte[] ResponseBody { get; private set; } = [];

    /// <summary>Starts the backend if needed and waits until its indices exist (<c>/health/ready</c>).</summary>
    public async Task WaitUntilReadyAsync()
    {
        using HttpClient client = Backend.CreateClient();
        DateTime deadline = DateTime.UtcNow.AddSeconds(60);
        while (true)
        {
            using HttpResponseMessage response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail($"Not ready after 60 s: {await response.Content.ReadAsStringAsync()}");
            }

            await Task.Delay(200);
        }
    }

    /// <summary>Sends a request to the backend and keeps the answer for the Then steps.</summary>
    public async Task SendAsync(HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);
        using HttpClient client = Backend.CreateClient(new() { AllowAutoRedirect = false });
        Response?.Dispose();
        Response = await client.SendAsync(request);
        ResponseBody = await Response.Content.ReadAsByteArrayAsync();
    }

    /// <summary>The backend under test, created on first use with the scenario's settings.</summary>
    public TonTuyauFactory Backend => _factory ??= new TonTuyauFactory(Settings.ToConfiguration(), ServiceOverrides);

    /// <summary>Stops the backend; the next use of <see cref="Backend"/> starts a new one with the same settings.</summary>
    public void StopBackend()
    {
        _factory?.Dispose();
        _factory = null;
    }

    public void Dispose()
    {
        Response?.Dispose();
        _factory?.Dispose();
    }
}
