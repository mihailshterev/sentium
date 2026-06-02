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
}
