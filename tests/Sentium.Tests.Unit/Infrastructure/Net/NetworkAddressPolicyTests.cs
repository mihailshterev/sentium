using System.Net;
using System.Net.Http;
using FluentAssertions;
using Sentium.Infrastructure.Net;
using Xunit;

namespace Sentium.Tests.Unit.Infrastructure.Net;

/// <summary>
/// The connect-time address guard: MCP clients may only reach the local network, a future fetch tool only the public
/// internet, and neither may touch link-local (cloud metadata) addresses.
/// </summary>
public sealed class NetworkAddressPolicyTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.10.0.5")]
    [InlineData("::1")]
    [InlineData("10.0.0.8")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.254")]
    [InlineData("192.168.1.20")]
    [InlineData("100.64.0.1")]
    [InlineData("100.127.255.254")]
    [InlineData("fd12:3456:789a::1")]
    [InlineData("::ffff:192.168.1.20")]
    public void PrivateOnly_AllowsLoopbackAndPrivateRanges(string address)
    {
        NetworkAddressPolicy.IsAllowed(IPAddress.Parse(address), NetworkAddressScope.PrivateOnly).Should().BeTrue();
        NetworkAddressPolicy.IsAllowed(IPAddress.Parse(address), NetworkAddressScope.PublicOnly).Should().BeFalse();
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("172.32.0.1")]
    [InlineData("100.128.0.1")]
    [InlineData("2606:4700:4700::1111")]
    [InlineData("::ffff:8.8.8.8")]
    public void PublicAddresses_AreOnlyAllowedForPublicScope(string address)
    {
        NetworkAddressPolicy.IsAllowed(IPAddress.Parse(address), NetworkAddressScope.PrivateOnly).Should().BeFalse();
        NetworkAddressPolicy.IsAllowed(IPAddress.Parse(address), NetworkAddressScope.PublicOnly).Should().BeTrue();
    }

    [Theory]
    [InlineData("169.254.169.254")]
    [InlineData("169.254.0.1")]
    [InlineData("fe80::1")]
    [InlineData("0.0.0.0")]
    [InlineData("::")]
    [InlineData("255.255.255.255")]
    [InlineData("224.0.0.1")]
    [InlineData("::ffff:169.254.169.254")]
    public void LinkLocalAndUnusableAddresses_AreNeverAllowed(string address)
    {
        NetworkAddressPolicy.IsAllowed(IPAddress.Parse(address), NetworkAddressScope.PrivateOnly).Should().BeFalse();
        NetworkAddressPolicy.IsAllowed(IPAddress.Parse(address), NetworkAddressScope.PublicOnly).Should().BeFalse();
    }

    [Fact]
    public async Task ConnectCallback_RefusesAHostThatResolvesOnlyToPublicAddresses()
    {
        var ct = TestContext.Current.CancellationToken;
        var resolverCalls = 0;
        using var handler = NetworkAddressPolicy.CreateHandler(
            NetworkAddressScope.PrivateOnly,
            (_, _) =>
            {
                resolverCalls++;
                return Task.FromResult(new[] { IPAddress.Parse("93.184.216.34") });
            });
        using var client = new HttpClient(handler);

        var act = () => client.GetAsync(new Uri("http://mcp.example.test:8080/mcp"), ct);

        var error = await act.Should().ThrowAsync<HttpRequestException>();
        FindBlocked(error.Which).Should().NotBeNull();
        resolverCalls.Should().Be(1, "the guard resolves the host itself instead of trusting a later lookup");
    }

    [Fact]
    public async Task ConnectCallback_RefusesAPublicLiteralWithoutResolving()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = NetworkAddressPolicy.CreateHandler(
            NetworkAddressScope.PrivateOnly,
            (_, _) => throw new InvalidOperationException("literals must not be resolved"));
        using var client = new HttpClient(handler);

        var act = () => client.GetAsync(new Uri("http://8.8.8.8/mcp"), ct);

        var error = await act.Should().ThrowAsync<HttpRequestException>();
        FindBlocked(error.Which).Should().NotBeNull();
    }

    [Fact]
    public void Handler_DisablesProxyAndRedirects()
    {
        using var handler = NetworkAddressPolicy.CreateHandler(NetworkAddressScope.PrivateOnly);

        handler.UseProxy.Should().BeFalse("a proxy would make the connect-time check meaningless");
        handler.AllowAutoRedirect.Should().BeFalse("a redirect must not lead outside the allowed scope");
        handler.ConnectCallback.Should().NotBeNull();
    }

    private static NetworkAddressBlockedException? FindBlocked(Exception? ex)
    {
        for (; ex is not null; ex = ex.InnerException)
        {
            if (ex is NetworkAddressBlockedException blocked)
            {
                return blocked;
            }
        }

        return null;
    }
}
