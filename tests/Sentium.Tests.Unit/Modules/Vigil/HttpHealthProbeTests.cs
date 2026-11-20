using System.Net;
using System.Text;
using NSubstitute;
using Sentium.Vigil.Features.Monitoring.Probes;
using Sentium.Vigil.Features.Monitoring;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Vigil;

// HttpClient / handler lifetimes are intentionally short-lived test fixtures.
#pragma warning disable CA2000

public sealed class HttpHealthProbeTests
{
    private static HttpHealthProbe BuildProbe(HttpStatusCode statusCode, string body)
    {
        var handler = new StubHandler(statusCode, body);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://svc") };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(client);
        return new HttpHealthProbe(factory, "Identity", "identity");
    }

    private static string Report(string overall, params (string name, string status)[] entries)
    {
        var entryJson = string.Join(",", entries.Select(e =>
            $"{{\"name\":\"{e.name}\",\"status\":\"{e.status}\",\"description\":null,\"durationMs\":0.5,\"tags\":[],\"exception\":null}}"));
        return $"{{\"status\":\"{overall}\",\"totalDurationMs\":1.0,\"entries\":[{entryJson}]}}";
    }

    [Fact]
    public async Task ProbeAsync_ParsesHealthyReport()
    {
        var probe = BuildProbe(HttpStatusCode.OK, Report("Healthy", ("self", "Healthy"), ("db", "Healthy")));

        var result = await probe.ProbeAsync(CancellationToken.None);

        Assert.Equal(ServiceStatus.Healthy, result.Status);
        Assert.Equal(ComponentKind.Service, result.Kind);
        Assert.Equal(2, result.Checks.Count);
        Assert.Null(result.Details);
    }

    [Fact]
    public async Task ProbeAsync_ParsesDegradedReport()
    {
        var probe = BuildProbe(HttpStatusCode.OK, Report("Degraded", ("self", "Healthy"), ("cache", "Degraded")));

        var result = await probe.ProbeAsync(CancellationToken.None);

        Assert.Equal(ServiceStatus.Degraded, result.Status);
        Assert.Contains("cache", result.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProbeAsync_ParsesUnhealthyReport_With503()
    {
        var probe = BuildProbe(HttpStatusCode.ServiceUnavailable, Report("Unhealthy", ("db", "Unhealthy")));

        var result = await probe.ProbeAsync(CancellationToken.None);

        Assert.Equal(ServiceStatus.Unhealthy, result.Status);
        Assert.Contains("db", result.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProbeAsync_FallsBackToStatusCode_WhenBodyNotJson()
    {
        var probe = BuildProbe(HttpStatusCode.OK, "Healthy");

        var result = await probe.ProbeAsync(CancellationToken.None);

        Assert.Equal(ServiceStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task ProbeAsync_ReturnsUnhealthy_OnException()
    {
        var handler = new ThrowingHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://svc") };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(client);
        var probe = new HttpHealthProbe(factory, "Identity", "identity");

        var result = await probe.ProbeAsync(CancellationToken.None);

        Assert.Equal(ServiceStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Details);
    }

    private sealed class StubHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("connection refused");
    }
}
