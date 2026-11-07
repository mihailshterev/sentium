using FluentAssertions;
using Sentium.Vigil.Features.Monitoring.Probes;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Vigil;

public sealed class EndpointResolverTests
{
    [Theory]
    [InlineData("localhost:6379", "localhost", 6379)]
    [InlineData("localhost:6379,password=abc", "localhost", 6379)]
    [InlineData("redis://cache-host:6380", "cache-host", 6380)]
    [InlineData("Host=postgres;Port=5433;Username=postgres;Password=x", "postgres", 5433)]
    [InlineData("Host=pg-primary,pg-replica;Database=sentiumdb", "pg-primary", 1)]
    [InlineData("Endpoint=http://azurite:10000", "azurite", 10000)]
    public void ResolveHostPort_ParsesVariousFormats(string conn, string host, int port)
    {
        var result = EndpointResolver.ResolveHostPort(conn, defaultPort: 1);

        result.Should().NotBeNull();
        result!.Value.Host.Should().Be(host);
        result.Value.Port.Should().Be(port);
    }

    [Fact]
    public void ResolveHostPort_UsesDefaultPort_WhenMissing()
    {
        var result = EndpointResolver.ResolveHostPort("justhost", defaultPort: 6379);

        result!.Value.Host.Should().Be("justhost");
        result.Value.Port.Should().Be(6379);
    }

    [Fact]
    public void ResolveHostPort_ReturnsNull_ForEmpty()
    {
        EndpointResolver.ResolveHostPort(null, 1).Should().BeNull();
        EndpointResolver.ResolveHostPort("  ", 1).Should().BeNull();
    }
}
