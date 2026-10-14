using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using TonTuyau.Api.Cli;

namespace TonTuyau.Api.UnitTests;

/// <summary>The command line's way to the backend, on a real Kestrel: a Unix socket and a port.</summary>
public sealed class CliSocketTests
{
    [Fact]
    public async Task Only_a_request_through_the_socket_passes_the_gate()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string socket = Path.Combine(Path.GetTempPath(), $"tontuyau-{Guid.NewGuid():N}.sock");
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls($"http://127.0.0.1:0;http://unix:{socket}");
        await using WebApplication app = builder.Build();
        SocketCliGate cliGate = new();
        app.MapGet("/gate", (HttpContext context) => cliGate.Allows(context) ? "socket" : "port");
        await app.StartAsync(cancellationToken);
        try
        {
            Uri gate = new("gate", UriKind.Relative);
            using HttpClient throughSocket = CliSocket.Client(socket);
            string port = app.Urls.First(u => u.Contains("127.0.0.1", StringComparison.Ordinal));
            using HttpClient throughPort = new() { BaseAddress = new Uri(port) };

            Assert.Equal("socket", await throughSocket.GetStringAsync(gate, cancellationToken));
            Assert.Equal("port", await throughPort.GetStringAsync(gate, cancellationToken));
        }
        finally
        {
            await app.StopAsync(cancellationToken);
            File.Delete(socket);
        }
    }

    [Theory]
    [InlineData("http://+:9000", null, "http://+:9000;http://unix:/run/t.sock")]
    [InlineData(null, "8080", "http://*:8080;http://unix:/run/t.sock")]
    [InlineData(null, null, "http://localhost:5000;http://unix:/run/t.sock")]
    public void The_socket_is_added_to_the_backend_s_own_addresses(string? urls, string? ports, string expected)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["urls"] = urls,
            ["http_ports"] = ports,
            ["CLI_SOCKET"] = "/run/t.sock",
        });

        CliSocket.ListenAlso(builder);

        Assert.Equal(expected, builder.WebHost.GetSetting(WebHostDefaults.ServerUrlsKey));
    }

    [Fact]
    public void An_empty_setting_turns_the_socket_off()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["CLI_SOCKET"] = "" })
            .Build();

        Assert.Null(CliSocket.PathFrom(configuration));
    }
}
