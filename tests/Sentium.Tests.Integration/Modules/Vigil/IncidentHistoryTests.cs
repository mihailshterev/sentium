using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Sentium.Vigil.Features.Monitoring;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Vigil;

/// <summary>
/// Incidents are written to the database, so their history and the ones still open survive a restart.
/// </summary>
public sealed class IncidentHistoryTests(MonolithWebApplicationFactory factory) : IClassFixture<MonolithWebApplicationFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ResolvedIncidents_OutliveTheProcess()
    {
        var target = $"target-{Guid.NewGuid():N}";
        var before = NewStore();
        await before.OpenAsync(target, ComponentKind.Service, IncidentSeverity.Critical, ServiceStatus.Unhealthy, "down", Ct);
        var resolved = await before.ResolveAsync(target, Ct);

        var history = await NewStore().GetRecentAsync(1000, Ct);

        var restored = Assert.Single(history, i => i.Id == resolved!.Id);
        Assert.Equal(IncidentStatus.Resolved, restored.Status);
        Assert.Equal("down", restored.Description);
        Assert.NotNull(restored.DurationMs);
    }

    [Fact]
    public async Task OpenIncidents_AreRestoredForMonitoredTargets_AndResolvedOtherwise()
    {
        var monitored = $"monitored-{Guid.NewGuid():N}";
        var gone = $"gone-{Guid.NewGuid():N}";
        var before = NewStore();
        var stillOpen = await before.OpenAsync(monitored, ComponentKind.Infrastructure, IncidentSeverity.Warning, ServiceStatus.Degraded, null, Ct);
        var orphaned = await before.OpenAsync(gone, ComponentKind.Service, IncidentSeverity.Critical, ServiceStatus.Unhealthy, null, Ct);

        var after = NewStore();
        await after.RestoreOpenAsync(new HashSet<string> { monitored }, Ct);

        Assert.Equal(stillOpen!.Id, after.GetOpen(monitored)?.Id);
        Assert.Null(after.GetOpen(gone));
        var history = await after.GetRecentAsync(1000, Ct);
        Assert.Contains(history, i => i.Id == orphaned!.Id && i.Status == IncidentStatus.Resolved);

        var recovered = await after.ResolveAsync(monitored, Ct);
        Assert.Equal(stillOpen.Id, recovered!.Id);
    }

    [Fact]
    public async Task Endpoint_ListsResolvedIncidentsFromTheDatabase()
    {
        var target = $"api-{Guid.NewGuid():N}";
        var store = NewStore();
        await store.OpenAsync(target, ComponentKind.Service, IncidentSeverity.Critical, ServiceStatus.Unhealthy, null, Ct);
        await store.ResolveAsync(target, Ct);

        using var client = factory.CreateClient();
        var incidents = await client.GetFromJsonAsync<List<IncidentView>>("/api/vigil/incidents?limit=1000", Ct);

        Assert.Contains(incidents!, i => i.Target == target && i.Status == "Resolved");
    }

    private IncidentStore NewStore()
    {
        return new IncidentStore(factory.Services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System, NullLogger<IncidentStore>.Instance);
    }

    private sealed record IncidentView(Guid Id, string Target, string Status);
}
