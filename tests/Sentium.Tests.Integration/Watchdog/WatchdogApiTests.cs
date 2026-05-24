using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Sentium.Tests.Integration.Common;
using Sentium.Watchdog.Core.Metrics;
using Xunit;

namespace Sentium.Tests.Integration.Watchdog;

public sealed class WatchdogApiTests(SentiumWebApplicationFactory<Sentium.Watchdog.Api.Program> factory)
    : IClassFixture<SentiumWebApplicationFactory<Sentium.Watchdog.Api.Program>>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_SystemMetrics_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/system/metrics", Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_SystemMetrics_ResponseHasExpectedShape()
    {
        // Act
        var response = await _client.GetAsync("/system/metrics", Ct);

        response.EnsureSuccessStatusCode();
        var metrics = await response.Content.ReadFromJsonAsync<SystemMetrics>(Ct);

        // Assert
        metrics.Should().NotBeNull();
        metrics!.Host.MachineName.Should().NotBeNullOrWhiteSpace();
        metrics.Memory.TotalMb.Should().BeGreaterThan(0);
        metrics.Process.Id.Should().BeGreaterThan(0);
    }
}
