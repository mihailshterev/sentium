using Sentium.Vigil.Features.Monitoring;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Vigil;

/// <summary>
/// The in-memory side of the store, which the monitoring loop relies on even while the database is down.
/// </summary>
public sealed class IncidentStoreTests
{
    private readonly IncidentStore _sut = OfflineIncidentStore.Create();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Open_CreatesIncident_WhenNoneOpen()
    {
        var incident = await _sut.OpenAsync("Identity", ComponentKind.Service, IncidentSeverity.Critical, ServiceStatus.Unhealthy, "down", Ct);

        Assert.NotNull(incident);
        Assert.Equal(IncidentStatus.Open, incident!.Status);
        Assert.Equal(1, _sut.OpenCount);
        Assert.NotNull(_sut.GetOpen("Identity"));
    }

    [Fact]
    public async Task Open_IsDeduplicated_WhenAlreadyOpen()
    {
        await _sut.OpenAsync("Identity", ComponentKind.Service, IncidentSeverity.Critical, ServiceStatus.Unhealthy, null, Ct);
        var second = await _sut.OpenAsync("Identity", ComponentKind.Service, IncidentSeverity.Warning, ServiceStatus.Degraded, null, Ct);

        Assert.Null(second);
        Assert.Equal(1, _sut.OpenCount);
    }

    [Fact]
    public async Task Resolve_ClosesIncident_AndSetsDuration()
    {
        await _sut.OpenAsync("Redis", ComponentKind.Infrastructure, IncidentSeverity.Critical, ServiceStatus.Unhealthy, null, Ct);

        var resolved = await _sut.ResolveAsync("Redis", Ct);

        Assert.NotNull(resolved);
        Assert.Equal(IncidentStatus.Resolved, resolved!.Status);
        Assert.NotNull(resolved.ResolvedAt);
        Assert.NotNull(resolved.DurationMs);
        Assert.Equal(0, _sut.OpenCount);
        Assert.Null(_sut.GetOpen("Redis"));
    }

    [Fact]
    public async Task Resolve_ReturnsNull_WhenNoneOpen()
    {
        Assert.Null(await _sut.ResolveAsync("Unknown", Ct));
    }

    [Fact]
    public async Task GetRecent_WithoutDatabase_ReturnsOpenAndResolvedFromMemory()
    {
        await _sut.OpenAsync("A", ComponentKind.Service, IncidentSeverity.Critical, ServiceStatus.Unhealthy, null, Ct);
        await _sut.OpenAsync("B", ComponentKind.Service, IncidentSeverity.Warning, ServiceStatus.Degraded, null, Ct);
        await _sut.ResolveAsync("A", Ct);

        var all = await _sut.GetRecentAsync(10, Ct);

        Assert.Equal(2, all.Count);
        Assert.Equal(1, all.Count(i => i.Status == IncidentStatus.Open));
        Assert.Equal(1, all.Count(i => i.Status == IncidentStatus.Resolved));
    }

    [Fact]
    public async Task Open_AfterResolve_StartsNewIncident()
    {
        await _sut.OpenAsync("A", ComponentKind.Service, IncidentSeverity.Critical, ServiceStatus.Unhealthy, null, Ct);
        await _sut.ResolveAsync("A", Ct);

        var reopened = await _sut.OpenAsync("A", ComponentKind.Service, IncidentSeverity.Critical, ServiceStatus.Unhealthy, null, Ct);

        Assert.NotNull(reopened);
        Assert.Equal(1, _sut.OpenCount);
    }
}
