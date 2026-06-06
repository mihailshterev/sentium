using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Sentium.Tests.Integration.Common;
using Sentium.Watchdog.Core.Metrics;
using Sentium.Watchdog.Core.Monitoring;
using Xunit;

namespace Sentium.Tests.Integration.Watchdog;

public sealed class WatchdogApiTests(SentiumWebApplicationFactory<Sentium.Watchdog.Api.Program> factory)
    : IClassFixture<SentiumWebApplicationFactory<Sentium.Watchdog.Api.Program>>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

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

    [Fact]
    public async Task Get_Status_ReturnsOk_WithList()
    {
        var response = await _client.GetAsync("/status", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var statuses = await response.Content.ReadFromJsonAsync<List<ServiceHealthStatus>>(JsonOptions, Ct);
        statuses.Should().NotBeNull();
    }

    [Fact]
    public async Task Get_Overview_ReturnsOk_WithAggregateShape()
    {
        var response = await _client.GetAsync("/status/overview", Ct);

        response.EnsureSuccessStatusCode();
        var overview = await response.Content.ReadFromJsonAsync<SystemHealthOverview>(JsonOptions, Ct);

        overview.Should().NotBeNull();
        overview!.Total.Should().Be(overview.Healthy + overview.Degraded + overview.Unhealthy + overview.Unknown);
    }

    [Fact]
    public async Task Get_Incidents_ReturnsOk_WithList()
    {
        var response = await _client.GetAsync("/incidents", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var incidents = await response.Content.ReadFromJsonAsync<List<Incident>>(JsonOptions, Ct);
        incidents.Should().NotBeNull();
    }

    [Fact]
    public async Task Get_Status_ForUnknownTarget_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/status/does-not-exist", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
