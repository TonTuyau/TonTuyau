using System.Net;
using TonTuyau.Api.Hosting;

namespace TonTuyau.Api.UnitTests;

/// <summary>Who the login's rate limit counts: the visitor, behind a named reverse proxy too, never a spoofed entry.</summary>
public sealed class ClientAddressesTests
{
    private static readonly ClientAddresses NoProxy = new(ClientAddresses.Parse(""));
    private static readonly ClientAddresses BehindProxy = new(ClientAddresses.Parse("192.168.1.1"));

    [Theory]
    [InlineData("203.0.113.5", "203.0.113.5")]
    [InlineData("1.2.3.4, 203.0.113.5", "203.0.113.5")]
    [InlineData("203.0.113.5, 192.168.1.1", "192.168.1.1")]
    public void Without_a_named_proxy_the_front_servers_entry_counts(string forwarded, string client)
    {
        Assert.Equal(client, NoProxy.Resolve([forwarded], IPAddress.Loopback));
    }

    [Theory]
    [InlineData("203.0.113.5, 192.168.1.1", "203.0.113.5")]
    [InlineData("203.0.113.5, ::ffff:192.168.1.1", "203.0.113.5")]
    [InlineData("::ffff:203.0.113.5, 192.168.1.1", "203.0.113.5")]
    [InlineData("1.2.3.4, 203.0.113.5, 192.168.1.1", "203.0.113.5")]
    [InlineData("192.168.1.1", "192.168.1.1")]
    [InlineData("203.0.113.5", "203.0.113.5")]
    public void Behind_a_named_proxy_its_entry_names_the_visitor(string forwarded, string client)
    {
        Assert.Equal(client, BehindProxy.Resolve([forwarded], IPAddress.Loopback));
    }

    [Fact]
    public void Several_headers_are_one_list()
    {
        Assert.Equal("203.0.113.5", BehindProxy.Resolve(["1.2.3.4", "203.0.113.5, 192.168.1.1"], IPAddress.Loopback));
    }

    [Fact]
    public void Without_the_header_the_connection_counts()
    {
        Assert.Equal("10.0.0.7", NoProxy.Resolve([], IPAddress.Parse("::ffff:10.0.0.7")));
    }

    [Fact]
    public void Several_proxies_are_separated_by_commas()
    {
        ClientAddresses two = new(ClientAddresses.Parse(" 192.168.1.1 , 10.0.0.2"));

        Assert.Equal("203.0.113.5", two.Resolve(["203.0.113.5, 192.168.1.1, 10.0.0.2"], IPAddress.Loopback));
    }
}
