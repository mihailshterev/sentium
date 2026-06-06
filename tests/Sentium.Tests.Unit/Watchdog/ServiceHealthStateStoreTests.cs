using FluentAssertions;
using Sentium.Watchdog.Application.Monitoring;
using Sentium.Watchdog.Core.Monitoring;
using Xunit;

namespace Sentium.Tests.Unit.Watchdog;

public sealed class ServiceHealthStateStoreTests
{
    private readonly ServiceHealthStateStore _sut = new();

    private static ServiceHealthStatus MakeStatus(string name = "gateway", ServiceStatus status = ServiceStatus.Healthy) =>
        new()
        {
            ServiceName = name,
            Status = status,
            LatencyMs = 10.5,
            CheckedAt = DateTimeOffset.UtcNow,
            Details = null
        };

    [Fact]
    public void GetAll_ReturnsEmpty_WhenNoStatusesAdded()
    {
        // Act
        var result = _sut.GetAll();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void UpdateStatus_StoresStatus_AndGetAllReturnsIt()
    {
        // Arrange
        var status = MakeStatus("agentruntime");

        // Act
        _sut.UpdateStatus(status);
        var result = _sut.GetAll();

        // Assert
        result.Should().ContainSingle()
            .Which.ServiceName.Should().Be("agentruntime");
    }

    [Fact]
    public void UpdateStatus_Overwrites_WhenSameServiceNameUpdatedTwice()
    {
        // Arrange
        var first = new ServiceHealthStatus
        {
            ServiceName = "watchdog",
            Status = ServiceStatus.Healthy,
            LatencyMs = 5,
            CheckedAt = DateTimeOffset.UtcNow
        };
        var second = new ServiceHealthStatus
        {
            ServiceName = "watchdog",
            Status = ServiceStatus.Unhealthy,
            LatencyMs = 999,
            CheckedAt = DateTimeOffset.UtcNow.AddSeconds(1)
        };

        // Act
        _sut.UpdateStatus(first);
        _sut.UpdateStatus(second);
        var result = _sut.GetAll();

        // Assert
        result.Should().ContainSingle()
            .Which.Status.Should().Be(ServiceStatus.Unhealthy);
    }

    [Fact]
    public void Get_ReturnsStatus_WhenServiceExists()
    {
        // Arrange
        var status = MakeStatus("sentinel");
        _sut.UpdateStatus(status);

        // Act
        var result = _sut.Get("sentinel");

        // Assert
        result.Should().NotBeNull();
        result!.ServiceName.Should().Be("sentinel");
    }

    [Fact]
    public void Get_ReturnsNull_WhenServiceNotFound()
    {
        // Act
        var result = _sut.Get("nonexistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetAll_OrdersAlphabetically_ByServiceName()
    {
        // Arrange
        _sut.UpdateStatus(MakeStatus("zebra"));
        _sut.UpdateStatus(MakeStatus("apple"));
        _sut.UpdateStatus(MakeStatus("mango"));

        // Act
        var result = _sut.GetAll();

        // Assert
        result.Select(s => s.ServiceName).Should().BeInAscendingOrder();
    }

    [Fact]
    public void GetAll_OrdersServicesBeforeInfrastructure()
    {
        _sut.UpdateStatus(MakeStatus("Redis") with { Kind = ComponentKind.Infrastructure });
        _sut.UpdateStatus(MakeStatus("Identity") with { Kind = ComponentKind.Service });

        var result = _sut.GetAll();

        result.Select(s => s.Kind).Should().ContainInOrder(ComponentKind.Service, ComponentKind.Infrastructure);
    }

    [Fact]
    public void UpdateStatus_ComputesUptimePercent_AcrossChecks()
    {
        // 3 healthy + 1 unhealthy = 75% up
        _sut.UpdateStatus(MakeStatus("svc", ServiceStatus.Healthy));
        _sut.UpdateStatus(MakeStatus("svc", ServiceStatus.Healthy));
        _sut.UpdateStatus(MakeStatus("svc", ServiceStatus.Unhealthy));
        var enriched = _sut.UpdateStatus(MakeStatus("svc", ServiceStatus.Healthy));

        enriched.UptimePercent.Should().Be(75);
    }

    [Fact]
    public void UpdateStatus_CountsDegradedAsUp_ForUptime()
    {
        _sut.UpdateStatus(MakeStatus("svc", ServiceStatus.Degraded));
        var enriched = _sut.UpdateStatus(MakeStatus("svc", ServiceStatus.Healthy));

        enriched.UptimePercent.Should().Be(100);
    }

    [Fact]
    public void UpdateStatus_IncrementsConsecutiveFailures_AndResetsOnHealthy()
    {
        _sut.UpdateStatus(MakeStatus("svc", ServiceStatus.Unhealthy));
        var two = _sut.UpdateStatus(MakeStatus("svc", ServiceStatus.Degraded));
        two.ConsecutiveFailures.Should().Be(2);

        var recovered = _sut.UpdateStatus(MakeStatus("svc", ServiceStatus.Healthy));
        recovered.ConsecutiveFailures.Should().Be(0);
    }

    [Fact]
    public void GetSamples_ReturnsRecentSamples_UpToTake()
    {
        for (var i = 0; i < 5; i++)
        {
            _sut.UpdateStatus(MakeStatus("svc"));
        }

        _sut.GetSamples("svc", 3).Should().HaveCount(3);
        _sut.GetSamples("svc", 100).Should().HaveCount(5);
        _sut.GetSamples("missing", 10).Should().BeEmpty();
    }
}
