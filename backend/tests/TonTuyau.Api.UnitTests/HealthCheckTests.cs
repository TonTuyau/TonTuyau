using System.Net;
using System.Net.Sockets;
using TonTuyau.Api.Cli;

namespace TonTuyau.Api.UnitTests;

public sealed class HealthCheckTests
{
    [Theory]
    [InlineData("8080", 8080)]
    [InlineData("5080;5081", 5080)]
    [InlineData(null, 8080)]
    [InlineData("", 8080)]
    public void The_port_is_the_first_of_the_http_ports(string? setting, int expected)
    {
        Assert.Equal(expected, HealthCheck.Port(setting));
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, CliRunner.Ok)]
    [InlineData(HttpStatusCode.ServiceUnavailable, CliRunner.RunFailed)]
    public async Task The_backend_s_answer_is_the_exit_code(HttpStatusCode status, int expected)
    {
        int port = FreePort();
        using HttpListener backend = new();
        backend.Prefixes.Add($"http://127.0.0.1:{port}/");
        backend.Start();
        Task answering = Task.Run(
            async () =>
            {
                HttpListenerContext request = await backend.GetContextAsync();
                Assert.Equal("/health", request.Request.Url?.AbsolutePath);
                request.Response.StatusCode = (int)status;
                request.Response.Close();
            },
            TestContext.Current.CancellationToken);
        using StringWriter error = new();

        int exitCode = await HealthCheck.AskAsync(port, error);

        await answering;
        Assert.Equal(expected, exitCode);
    }

    [Fact]
    public async Task Nobody_answering_is_unhealthy_and_says_so()
    {
        using StringWriter error = new();

        int exitCode = await HealthCheck.AskAsync(FreePort(), error);

        Assert.Equal(CliRunner.RunFailed, exitCode);
        Assert.StartsWith("The backend doesn't answer", error.ToString(), StringComparison.Ordinal);
    }

    private static int FreePort()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
