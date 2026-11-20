using Sentium.Vigil.Features.Monitoring;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Vigil;

public sealed class IncidentStoreTests
{
    private readonly IncidentStore _sut = new();

    [Fact]
    public void Open_CreatesIncident_WhenNoneOpen()
    {
        var incident = _sut.Open("Identity", ComponentKind.Service, IncidentSeverity.Critical, ServiceStatus.Unhealthy, "down");

        Assert.NotNull(incident);
        Assert.Equal(IncidentStatus.Open, incident!.Status);
        Assert.Equal(1, _sut.OpenCount);
        Assert.NotNull(_sut.GetOpen("Identity"));
    }

    [Fact]
    public void Open_IsDeduplicated_WhenAlreadyOpen()
    {
        _sut.Open("Identity", ComponentKind.Service, IncidentSeverity.Critical, ServiceStatus.Unhealthy, null);
        var second = _sut.Open("Identity", ComponentKind.Service, IncidentSeverity.Warning, ServiceStatus.Degraded, null);

        Assert.Null(second);
        Assert.Equal(1, _sut.OpenCount);
    }

    [Fact]
    public void Resolve_ClosesIncident_AndSetsDuration()
    {
        _sut.Open("Redis", ComponentKind.Infrastructure, IncidentSeverity.Critical, ServiceStatus.Unhealthy, null);

        var resolved = _sut.Resolve("Redis");

        Assert.NotNull(resolved);
        Assert.Equal(IncidentStatus.Resolved, resolved!.Status);
        Assert.NotNull(resolved.ResolvedAt);
        Assert.NotNull(resolved.DurationMs);
        Assert.Equal(0, _sut.OpenCount);
        Assert.Null(_sut.GetOpen("Redis"));
    }

    [Fact]
    public void Resolve_ReturnsNull_WhenNoneOpen()
    {
        Assert.Null(_sut.Resolve("Unknown"));
    }

    [Fact]
    public void GetAll_ReturnsOpenAndResolved()
    {
        _sut.Open("A", ComponentKind.Service, IncidentSeverity.Critical, ServiceStatus.Unhealthy, null);
        _sut.Open("B", ComponentKind.Service, IncidentSeverity.Warning, ServiceStatus.Degraded, null);
        _sut.Resolve("A");

        var all = _sut.GetAll();

        Assert.Equal(2, all.Count);
        Assert.Equal(1, all.Count(i => i.Status == IncidentStatus.Open));
        Assert.Equal(1, all.Count(i => i.Status == IncidentStatus.Resolved));
    }

    [Fact]
    public void Open_AfterResolve_StartsNewIncident()
    {
        _sut.Open("A", ComponentKind.Service, IncidentSeverity.Critical, ServiceStatus.Unhealthy, null);
        _sut.Resolve("A");

        var reopened = _sut.Open("A", ComponentKind.Service, IncidentSeverity.Critical, ServiceStatus.Unhealthy, null);

        Assert.NotNull(reopened);
        Assert.Equal(1, _sut.OpenCount);
    }
}
