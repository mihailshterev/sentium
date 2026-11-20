using Sentium.Vigil.Features.Monitoring;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Vigil;

public sealed class ServiceHealthStateStoreTests
{
    private readonly ServiceHealthStateStore _sut = new();

    private static ServiceHealthStatus MakeStatus(string name = "gateway", ServiceStatus status = ServiceStatus.Healthy)
    {
        return new()
        {
            ServiceName = name,
            Status = status,
            LatencyMs = 10.5,
            CheckedAt = DateTimeOffset.UtcNow,
            Details = null
        };
    }

    [Fact]
    public void GetAll_ReturnsEmpty_WhenNoStatusesAdded()
    {
        // Act
        var result = _sut.GetAll();

        // Assert
        Assert.Empty(result);
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
        Assert.Equal("agentruntime", Assert.Single(result).ServiceName);
    }

    [Fact]
    public void UpdateStatus_Overwrites_WhenSameServiceNameUpdatedTwice()
    {
        // Arrange
        var first = new ServiceHealthStatus
        {
            ServiceName = "vigil",
            Status = ServiceStatus.Healthy,
            LatencyMs = 5,
            CheckedAt = DateTimeOffset.UtcNow
        };
        var second = new ServiceHealthStatus
        {
            ServiceName = "vigil",
            Status = ServiceStatus.Unhealthy,
            LatencyMs = 999,
            CheckedAt = DateTimeOffset.UtcNow.AddSeconds(1)
        };

        // Act
        _sut.UpdateStatus(first);
        _sut.UpdateStatus(second);
        var result = _sut.GetAll();

        // Assert
        Assert.Equal(ServiceStatus.Unhealthy, Assert.Single(result).Status);
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
        Assert.NotNull(result);
        Assert.Equal("sentinel", result!.ServiceName);
    }

    [Fact]
    public void Get_ReturnsNull_WhenServiceNotFound()
    {
        // Act
        var result = _sut.Get("nonexistent");

        // Assert
        Assert.Null(result);
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
        var names = result.Select(s => s.ServiceName).ToList();
        Assert.Equal(names.Order(), names);
    }

    [Fact]
    public void GetAll_OrdersServicesBeforeInfrastructure()
    {
        _sut.UpdateStatus(MakeStatus("Redis") with { Kind = ComponentKind.Infrastructure });
        _sut.UpdateStatus(MakeStatus("Identity") with { Kind = ComponentKind.Service });

        var result = _sut.GetAll();

        var kinds = result.Select(s => s.Kind).ToList();
        var service = kinds.IndexOf(ComponentKind.Service);
        Assert.True(service >= 0 && kinds.IndexOf(ComponentKind.Infrastructure, service + 1) > service);
    }

    [Fact]
    public void UpdateStatus_ComputesUptimePercent_AcrossChecks()
    {
        // 3 healthy + 1 unhealthy = 75% up
        _sut.UpdateStatus(MakeStatus("svc", ServiceStatus.Healthy));
        _sut.UpdateStatus(MakeStatus("svc", ServiceStatus.Healthy));
        _sut.UpdateStatus(MakeStatus("svc", ServiceStatus.Unhealthy));
        var enriched = _sut.UpdateStatus(MakeStatus("svc", ServiceStatus.Healthy));

        Assert.Equal(75, enriched.UptimePercent);
    }

    [Fact]
    public void UpdateStatus_CountsDegradedAsUp_ForUptime()
    {
        _sut.UpdateStatus(MakeStatus("svc", ServiceStatus.Degraded));
        var enriched = _sut.UpdateStatus(MakeStatus("svc", ServiceStatus.Healthy));

        Assert.Equal(100, enriched.UptimePercent);
    }

    [Fact]
    public void UpdateStatus_IncrementsConsecutiveFailures_AndResetsOnHealthy()
    {
        _sut.UpdateStatus(MakeStatus("svc", ServiceStatus.Unhealthy));
        var two = _sut.UpdateStatus(MakeStatus("svc", ServiceStatus.Degraded));
        Assert.Equal(2, two.ConsecutiveFailures);

        var recovered = _sut.UpdateStatus(MakeStatus("svc", ServiceStatus.Healthy));
        Assert.Equal(0, recovered.ConsecutiveFailures);
    }

    [Fact]
    public void GetSamples_ReturnsRecentSamples_UpToTake()
    {
        for (var i = 0; i < 5; i++)
        {
            _sut.UpdateStatus(MakeStatus("svc"));
        }

        Assert.Equal(3, _sut.GetSamples("svc", 3).Count);
        Assert.Equal(5, _sut.GetSamples("svc", 100).Count);
        Assert.Empty(_sut.GetSamples("missing", 10));
    }
}
