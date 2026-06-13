using FluentAssertions;
using Sentium.Watchdog.Application.Monitoring;
using Sentium.Watchdog.Core.Monitoring;
using Xunit;

namespace Sentium.Tests.Unit.Watchdog;

public sealed class IncidentStoreTests
{
    private readonly IncidentStore _sut = new();

    [Fact]
    public void Open_CreatesIncident_WhenNoneOpen()
    {
        var incident = _sut.Open("Identity", ComponentKind.Service, IncidentSeverity.Critical, ServiceStatus.Unhealthy, "down");

        incident.Should().NotBeNull();
        incident!.Status.Should().Be(IncidentStatus.Open);
        _sut.OpenCount.Should().Be(1);
        _sut.GetOpen("Identity").Should().NotBeNull();
    }

    [Fact]
    public void Open_IsDeduplicated_WhenAlreadyOpen()
    {
        _sut.Open("Identity", ComponentKind.Service, IncidentSeverity.Critical, ServiceStatus.Unhealthy, null);
        var second = _sut.Open("Identity", ComponentKind.Service, IncidentSeverity.Warning, ServiceStatus.Degraded, null);

        second.Should().BeNull();
        _sut.OpenCount.Should().Be(1);
    }

    [Fact]
    public void Resolve_ClosesIncident_AndSetsDuration()
    {
        _sut.Open("Redis", ComponentKind.Infrastructure, IncidentSeverity.Critical, ServiceStatus.Unhealthy, null);

        var resolved = _sut.Resolve("Redis");

        resolved.Should().NotBeNull();
        resolved!.Status.Should().Be(IncidentStatus.Resolved);
        resolved.ResolvedAt.Should().NotBeNull();
        resolved.DurationMs.Should().NotBeNull();
        _sut.OpenCount.Should().Be(0);
        _sut.GetOpen("Redis").Should().BeNull();
    }

    [Fact]
    public void Resolve_ReturnsNull_WhenNoneOpen()
    {
        _sut.Resolve("Unknown").Should().BeNull();
    }

    [Fact]
    public void GetAll_ReturnsOpenAndResolved()
    {
        _sut.Open("A", ComponentKind.Service, IncidentSeverity.Critical, ServiceStatus.Unhealthy, null);
        _sut.Open("B", ComponentKind.Service, IncidentSeverity.Warning, ServiceStatus.Degraded, null);
        _sut.Resolve("A");

        var all = _sut.GetAll();

        all.Should().HaveCount(2);
        all.Count(i => i.Status == IncidentStatus.Open).Should().Be(1);
        all.Count(i => i.Status == IncidentStatus.Resolved).Should().Be(1);
    }

    [Fact]
    public void Open_AfterResolve_StartsNewIncident()
    {
        _sut.Open("A", ComponentKind.Service, IncidentSeverity.Critical, ServiceStatus.Unhealthy, null);
        _sut.Resolve("A");

        var reopened = _sut.Open("A", ComponentKind.Service, IncidentSeverity.Critical, ServiceStatus.Unhealthy, null);

        reopened.Should().NotBeNull();
        _sut.OpenCount.Should().Be(1);
    }
}
