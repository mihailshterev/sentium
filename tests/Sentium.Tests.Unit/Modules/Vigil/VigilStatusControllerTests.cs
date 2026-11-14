using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Sentium.Vigil.Features.Monitoring;
using Sentium.Vigil.Features.Settings;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Vigil;

public sealed class VigilStatusControllerTests
{
    private readonly IServiceHealthStateStore _stateStore = Substitute.For<IServiceHealthStateStore>();
    private readonly IIncidentStore _incidentStore = Substitute.For<IIncidentStore>();
    private readonly IVigilSettingsProvider _settings = Substitute.For<IVigilSettingsProvider>();
    private readonly VigilStatusController _controller;

    public VigilStatusControllerTests()
    {
        _settings.GetAsync(Arg.Any<CancellationToken>()).Returns(new VigilRuntimeSettings());
        _controller = new VigilStatusController(_stateStore, _incidentStore, _settings);
    }

    private static ServiceHealthStatus MakeStatus(string name = "gateway", ComponentKind kind = ComponentKind.Service)
    {
        return new()
        {
            ServiceName = name,
            Kind = kind,
            Status = ServiceStatus.Healthy,
            LatencyMs = 10,
            CheckedAt = DateTimeOffset.UtcNow
        };
    }

    [Fact]
    public void GetAll_ReturnsOk_WithAllStatuses()
    {
        var statuses = new List<ServiceHealthStatus> { MakeStatus("gateway"), MakeStatus("sentinel") };
        _stateStore.GetAll().Returns(statuses);

        var result = _controller.GetAll();

        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeEquivalentTo(statuses);
    }

    [Fact]
    public async Task Get_ReturnsOk_WithDetailAndSamples_WhenServiceFound()
    {
        var status = MakeStatus("agentruntime");
        var samples = new List<HealthSample> { new(DateTimeOffset.UtcNow, ServiceStatus.Healthy, 12) };
        _stateStore.Get("agentruntime").Returns(status);
        _stateStore.GetSamples("agentruntime", Arg.Any<int>()).Returns(samples);

        var result = await _controller.Get("agentruntime", CancellationToken.None);

        var detail = result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<ServiceHealthDetail>().Subject;
        detail.Status.Should().Be(status);
        detail.Samples.Should().BeEquivalentTo(samples);
    }

    [Fact]
    public async Task Get_ReturnsNotFound_WhenServiceMissing()
    {
        _stateStore.Get("nonexistent").Returns((ServiceHealthStatus?)null);

        var result = await _controller.Get("nonexistent", CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void GetOverview_ReturnsOk_WithAggregatedCounts()
    {
        _stateStore.GetAll().Returns(new List<ServiceHealthStatus>
        {
            MakeStatus("a"),
            MakeStatus("b") with { Status = ServiceStatus.Unhealthy }
        });
        _incidentStore.OpenCount.Returns(1);

        var result = _controller.GetOverview();

        var overview = result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<SystemHealthOverview>().Subject;
        overview.Total.Should().Be(2);
        overview.Unhealthy.Should().Be(1);
        overview.OverallStatus.Should().Be(ServiceStatus.Unhealthy);
        overview.OpenIncidents.Should().Be(1);
    }
}
