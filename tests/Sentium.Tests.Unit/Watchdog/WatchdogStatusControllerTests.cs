using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Sentium.Watchdog.Api.Controllers;
using Sentium.Watchdog.Core.Monitoring;
using Xunit;

namespace Sentium.Tests.Unit.Watchdog;

public sealed class WatchdogStatusControllerTests
{
    private readonly IServiceHealthStateStore _stateStore = Substitute.For<IServiceHealthStateStore>();
    private readonly WatchdogStatusController _controller;

    public WatchdogStatusControllerTests()
    {
        _controller = new WatchdogStatusController(_stateStore);
    }

    private static ServiceHealthStatus MakeStatus(string name = "gateway") =>
        new()
        {
            ServiceName = name,
            Status = ServiceStatus.Healthy,
            LatencyMs = 10,
            CheckedAt = DateTimeOffset.UtcNow
        };

    [Fact]
    public void GetAll_ReturnsOk_WithAllStatuses()
    {
        // Arrange
        var statuses = new List<ServiceHealthStatus> { MakeStatus("gateway"), MakeStatus("sentinel") };
        _stateStore.GetAll().Returns(statuses);

        // Act
        var result = _controller.GetAll();

        // Assert
        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeEquivalentTo(statuses);
    }

    [Fact]
    public void GetAll_ReturnsOk_WithEmptyList_WhenNoServicesRegistered()
    {
        // Arrange
        _stateStore.GetAll().Returns(new List<ServiceHealthStatus>());

        // Act
        var result = _controller.GetAll();

        // Assert
        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.As<IReadOnlyList<ServiceHealthStatus>>().Should().BeEmpty();
    }

    [Fact]
    public void Get_ReturnsOk_WhenServiceFound()
    {
        // Arrange
        var status = MakeStatus("agentruntime");
        _stateStore.Get("agentruntime").Returns(status);

        // Act
        var result = _controller.Get("agentruntime");

        // Assert
        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().Be(status);
    }

    [Fact]
    public void Get_ReturnsNotFound_WhenServiceMissing()
    {
        // Arrange
        _stateStore.Get("nonexistent").Returns((ServiceHealthStatus?)null);

        // Act
        var result = _controller.Get("nonexistent");

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }
}
