using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sentium.Tests.Integration.Common;
using Sentium.Vigil.Features.Metrics;
using Sentium.Vigil.Features.Monitoring;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Vigil;

public sealed class VigilApiTests(MonolithWebApplicationFactory factory)
    : IClassFixture<MonolithWebApplicationFactory>
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
        var response = await _client.GetAsync("/api/vigil/system/metrics", Ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_SystemMetrics_ResponseHasExpectedShape()
    {
        // Act
        var response = await _client.GetAsync("/api/vigil/system/metrics", Ct);

        response.EnsureSuccessStatusCode();
        var metrics = await response.Content.ReadFromJsonAsync<SystemMetrics>(Ct);

        // Assert
        Assert.NotNull(metrics);
        Assert.False(string.IsNullOrWhiteSpace(metrics!.Host.MachineName));
        Assert.True(metrics.Memory.TotalMb > 0);
        Assert.True(metrics.Process.Id > 0);
    }

    [Fact]
    public async Task Get_Status_ReturnsOk_WithList()
    {
        var response = await _client.GetAsync("/api/vigil/status", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var statuses = await response.Content.ReadFromJsonAsync<List<ServiceHealthStatus>>(JsonOptions, Ct);
        Assert.NotNull(statuses);
    }

    [Fact]
    public async Task Get_Overview_ReturnsOk_WithAggregateShape()
    {
        var response = await _client.GetAsync("/api/vigil/status/overview", Ct);

        response.EnsureSuccessStatusCode();
        var overview = await response.Content.ReadFromJsonAsync<SystemHealthOverview>(JsonOptions, Ct);

        Assert.NotNull(overview);
        Assert.Equal(overview.Healthy + overview.Degraded + overview.Unhealthy + overview.Unknown, overview!.Total);
    }

    [Fact]
    public async Task Get_Incidents_ReturnsOk_WithList()
    {
        var response = await _client.GetAsync("/api/vigil/incidents", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var incidents = await response.Content.ReadFromJsonAsync<List<Incident>>(JsonOptions, Ct);
        Assert.NotNull(incidents);
    }

    [Fact]
    public async Task Get_Status_ForUnknownTarget_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/vigil/status/does-not-exist", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
