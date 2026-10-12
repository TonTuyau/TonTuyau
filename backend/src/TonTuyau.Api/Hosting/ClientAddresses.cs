using System.Net;

namespace TonTuyau.Api.Hosting;

/// <summary>
/// Who is asking, for the login's rate limit. The backend is only reached through the front server, which adds the
/// address it got the request from at the end of <c>X-Forwarded-For</c>. That last entry is the one to trust; behind
/// the user's reverse proxy it's the proxy's, so the entry before it is the visitor's, when the proxy is named in
/// <c>TONTUYAU_TRUSTED_PROXIES</c>. From the end, each address of a named proxy is skipped: the first other one is the
/// client. What comes before it, anyone could have written, so it's never read (a spoofed address doesn't count).
/// </summary>
public sealed class ClientAddresses(IReadOnlySet<IPAddress> trustedProxies)
{
    /// <summary>The trusted proxies of <c>TONTUYAU_TRUSTED_PROXIES</c>: addresses separated by commas (or empty).</summary>
    public static IReadOnlySet<IPAddress> Parse(string trustedProxies)
    {
        ArgumentNullException.ThrowIfNull(trustedProxies);
        return trustedProxies
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(IPAddress.Parse)
            .Select(Normalize)
            .ToHashSet();
    }

    public string Of(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Resolve(
            context.Request.Headers["X-Forwarded-For"],
            context.Connection.RemoteIpAddress);
    }

    internal string Resolve(IEnumerable<string?> forwardedFor, IPAddress? remote)
    {
        string[] chain = forwardedFor
            .SelectMany(value => (value ?? "").Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToArray();
        for (int index = chain.Length - 1; index >= 0; index--)
        {
            bool isProxy = IPAddress.TryParse(chain[index], out IPAddress? address)
                && trustedProxies.Contains(Normalize(address));
            if (!isProxy || index == 0)
            {
                return address is null ? chain[index] : Normalize(address).ToString();
            }
        }

        return remote is null ? "" : Normalize(remote).ToString();
    }

    // Node writes an IPv4 client as ::ffff:192.168.1.10 on a dual-stack socket: the same address as 192.168.1.10.
    private static IPAddress Normalize(IPAddress address) =>
        address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
}
