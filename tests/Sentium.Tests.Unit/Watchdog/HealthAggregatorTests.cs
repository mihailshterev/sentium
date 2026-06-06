using FluentAssertions;
using Sentium.Watchdog.Application.Monitoring;
using Sentium.Watchdog.Core.Monitoring;
using Xunit;

namespace Sentium.Tests.Unit.Watchdog;

public sealed class HealthAggregatorTests
{
    private static ServiceHealthStatus Status(ServiceStatus status) => new()
    {
        ServiceName = status.ToString(),
        Status = status,
        LatencyMs = 1,
        CheckedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public void BuildOverview_AllHealthy_IsHealthy()
    {
        var overview = HealthAggregator.BuildOverview([Status(ServiceStatus.Healthy), Status(ServiceStatus.Healthy)], 0);

        overview.OverallStatus.Should().Be(ServiceStatus.Healthy);
        overview.Healthy.Should().Be(2);
        overview.Total.Should().Be(2);
    }

    [Fact]
    public void BuildOverview_AnyUnhealthy_IsUnhealthy()
    {
        var overview = HealthAggregator.BuildOverview(
            [Status(ServiceStatus.Healthy), Status(ServiceStatus.Degraded), Status(ServiceStatus.Unhealthy)], 2);

        overview.OverallStatus.Should().Be(ServiceStatus.Unhealthy);
        overview.OpenIncidents.Should().Be(2);
    }

    [Fact]
    public void BuildOverview_DegradedButNoUnhealthy_IsDegraded()
    {
        var overview = HealthAggregator.BuildOverview([Status(ServiceStatus.Healthy), Status(ServiceStatus.Degraded)], 0);

        overview.OverallStatus.Should().Be(ServiceStatus.Degraded);
    }

    [Fact]
    public void BuildOverview_Empty_IsUnknown()
    {
        var overview = HealthAggregator.BuildOverview([], 0);

        overview.OverallStatus.Should().Be(ServiceStatus.Unknown);
        overview.Total.Should().Be(0);
    }

    [Fact]
    public void BuildOverview_WithUnknown_NotAllHealthy_IsUnknown()
    {
        var overview = HealthAggregator.BuildOverview([Status(ServiceStatus.Healthy), Status(ServiceStatus.Unknown)], 0);

        overview.OverallStatus.Should().Be(ServiceStatus.Unknown);
    }
}
