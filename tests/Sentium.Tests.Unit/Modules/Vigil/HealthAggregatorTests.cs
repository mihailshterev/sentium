using Sentium.Vigil.Features.Monitoring;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Vigil;

public sealed class HealthAggregatorTests
{
    private static ServiceHealthStatus Status(ServiceStatus status)
    {
        return new()
        {
            ServiceName = status.ToString(),
            Status = status,
            LatencyMs = 1,
            CheckedAt = DateTimeOffset.UtcNow
        };
    }

    [Fact]
    public void BuildOverview_AllHealthy_IsHealthy()
    {
        var overview = HealthAggregator.BuildOverview([Status(ServiceStatus.Healthy), Status(ServiceStatus.Healthy)], 0);

        Assert.Equal(ServiceStatus.Healthy, overview.OverallStatus);
        Assert.Equal(2, overview.Healthy);
        Assert.Equal(2, overview.Total);
    }

    [Fact]
    public void BuildOverview_AnyUnhealthy_IsUnhealthy()
    {
        var overview = HealthAggregator.BuildOverview(
            [Status(ServiceStatus.Healthy), Status(ServiceStatus.Degraded), Status(ServiceStatus.Unhealthy)], 2);

        Assert.Equal(ServiceStatus.Unhealthy, overview.OverallStatus);
        Assert.Equal(2, overview.OpenIncidents);
    }

    [Fact]
    public void BuildOverview_DegradedButNoUnhealthy_IsDegraded()
    {
        var overview = HealthAggregator.BuildOverview([Status(ServiceStatus.Healthy), Status(ServiceStatus.Degraded)], 0);

        Assert.Equal(ServiceStatus.Degraded, overview.OverallStatus);
    }

    [Fact]
    public void BuildOverview_Empty_IsUnknown()
    {
        var overview = HealthAggregator.BuildOverview([], 0);

        Assert.Equal(ServiceStatus.Unknown, overview.OverallStatus);
        Assert.Equal(0, overview.Total);
    }

    [Fact]
    public void BuildOverview_WithUnknown_NotAllHealthy_IsUnknown()
    {
        var overview = HealthAggregator.BuildOverview([Status(ServiceStatus.Healthy), Status(ServiceStatus.Unknown)], 0);

        Assert.Equal(ServiceStatus.Unknown, overview.OverallStatus);
    }
}
