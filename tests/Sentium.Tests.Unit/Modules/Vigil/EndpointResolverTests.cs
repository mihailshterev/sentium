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
    [InlineData("Endpoint=http://storage:8333;AccessKey=a;SecretKey=b", "storage", 8333)]
    public void ResolveHostPort_ParsesVariousFormats(string conn, string host, int port)
    {
        var result = EndpointResolver.ResolveHostPort(conn, defaultPort: 1);

        Assert.NotNull(result);
        Assert.Equal(host, result!.Value.Host);
        Assert.Equal(port, result.Value.Port);
    }

    [Fact]
    public void ResolveHostPort_UsesDefaultPort_WhenMissing()
    {
        var result = EndpointResolver.ResolveHostPort("justhost", defaultPort: 6379);

        Assert.Equal("justhost", result!.Value.Host);
        Assert.Equal(6379, result.Value.Port);
    }

    [Fact]
    public void ResolveHostPort_ReturnsNull_ForEmpty()
    {
        Assert.Null(EndpointResolver.ResolveHostPort(null, 1));
        Assert.Null(EndpointResolver.ResolveHostPort("  ", 1));
    }
}
