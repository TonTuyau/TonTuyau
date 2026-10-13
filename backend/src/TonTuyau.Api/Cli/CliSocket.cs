using System.Net.Sockets;
using TonTuyau.Application.Configuration;

namespace TonTuyau.Api.Cli;

/// <summary>
/// The Unix socket the command line reaches the backend through (1.4): the runs happen in the backend, so they're in
/// its log, and the Tasks page sees and cancels them. A file in the container (<see cref="DefaultPath"/>,
/// <c>TONTUYAU_CLI_SOCKET</c>): only a process inside it can use it; nothing listens on a port for it.
/// </summary>
internal static class CliSocket
{
    public const string DefaultPath = "/tmp/tontuyau.sock";

    /// <returns>The socket's path; null when it's off (<c>TONTUYAU_CLI_SOCKET</c> set empty).</returns>
    public static string? PathFrom(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        string? path = configuration[ConfigKeys.CliSocket] ?? DefaultPath;
        return path.Length == 0 ? null : path;
    }

    /// <summary>
    /// The backend listens on the socket too, besides its own address: Kestrel takes one list of addresses
    /// (<c>ASPNETCORE_URLS</c>, else <c>ASPNETCORE_HTTP_PORTS</c>, else its default), so the socket is added to it. A
    /// socket file left by a backend that didn't stop cleanly is removed first (a socket can't be opened on it).
    /// </summary>
    public static void ListenAlso(WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        string? path = PathFrom(builder.Configuration);
        if (path is null)
        {
            return;
        }

        string? urls = builder.Configuration["urls"];
        string? ports = builder.Configuration["http_ports"];
        string addresses = !string.IsNullOrEmpty(urls)
            ? urls
            : !string.IsNullOrEmpty(ports)
                ? string.Join(';', ports.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(p => $"http://*:{p}"))
                : "http://localhost:5000";
        File.Delete(path);
        builder.WebHost.UseUrls($"{addresses};http://unix:{path}");
    }

    /// <summary>An HTTP client to the backend through the socket (the command line's).</summary>
    public static HttpClient Client(string path)
    {
        SocketsHttpHandler handler = new()
        {
            ConnectCallback = async (_, cancellationToken) =>
            {
                Socket socket = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                try
                {
                    await socket
                        .ConnectAsync(new UnixDomainSocketEndPoint(path), cancellationToken)
                        .ConfigureAwait(false);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            },
        };
        return new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
    }
}
