namespace TonTuyau.Api.Cli;

/// <summary>
/// Only a request that came through the backend's Unix socket (<see cref="CliSocket"/>): a process inside the
/// container. Kestrel gives such a connection no IP address at all, local or remote, when one through the port (the
/// front's, anyone's) always has both. (Tests' in-memory server has none either: it passes, like the socket.)
/// </summary>
internal sealed class SocketCliGate : ICliGate
{
    public bool Allows(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Connection.RemoteIpAddress is null && context.Connection.LocalIpAddress is null;
    }
}
