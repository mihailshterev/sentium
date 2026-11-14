using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Locus.Data;
using Sentium.Locus.Features.Assets;
using Sentium.Locus.Features.Locations;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Locus;

public sealed class LocusLocationApiTests(LocusApiFactory factory) : IClassFixture<LocusApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateLocations_ReturnsTreePositionAndPath()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        var garage = await _client.CreateLocationAsync("Garage", home.Id, Ct);

        garage.Path.Should().Be($"{home.Name} › Garage");
        garage.Depth.Should().Be(2);
        garage.ParentLocationId.Should().Be(home.Id);
        garage.UpdatedBy.Should().Be("TestUser");

        var all = await _client.GetFromJsonAsync<List<LocationDto>>("/api/locus/locations", Ct);
        all!.Single(l => l.Id == home.Id).ChildCount.Should().Be(1);
    }

    [Fact]
    public async Task SiblingNames_AreUniqueCaseInsensitively()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        await _client.CreateLocationAsync("Kitchen", home.Id, Ct);

        var duplicate = await _client.PostAsJsonAsync("/api/locus/locations", new SaveLocationRequest("  KITCHEN ", null, null, home.Id), Ct);

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RootNames_AreUniqueToo()
    {
        var name = LocusApi.Unique("Root");
        await _client.CreateLocationAsync(name, ct: Ct);

        var duplicate = await _client.PostAsJsonAsync("/api/locus/locations", new SaveLocationRequest(name.ToUpperInvariant(), null, null, null), Ct);

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task SameName_UnderDifferentParents_IsAllowed()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        var cabin = await _client.CreateLocationAsync(LocusApi.Unique("Cabin"), ct: Ct);

        await _client.CreateLocationAsync("Kitchen", home.Id, Ct);
        var second = await _client.PostAsJsonAsync("/api/locus/locations", new SaveLocationRequest("Kitchen", null, null, cabin.Id), Ct);

        second.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task UnknownParent_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/locus/locations", new SaveLocationRequest("Orphan", null, null, Guid.NewGuid()), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MovingALocationIntoItsOwnSubtree_Returns409()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        var garage = await _client.CreateLocationAsync("Garage", home.Id, Ct);
        var shelf = await _client.CreateLocationAsync("Shelf", garage.Id, Ct);

        var response = await _client.PutAsJsonAsync($"/api/locus/locations/{home.Id}", new SaveLocationRequest(home.Name, null, null, shelf.Id), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task MovingALocation_UpdatesTheWholeSubtreePath()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        var shed = await _client.CreateLocationAsync("Shed", home.Id, Ct);
        var garage = await _client.CreateLocationAsync("Garage", home.Id, Ct);
        var shelf = await _client.CreateLocationAsync("Shelf", garage.Id, Ct);

        var response = await _client.PutAsJsonAsync($"/api/locus/locations/{garage.Id}", new SaveLocationRequest("Garage", null, null, shed.Id), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var moved = await _client.GetFromJsonAsync<LocationDto>($"/api/locus/locations/{shelf.Id}", Ct);
        moved!.Path.Should().Be($"{home.Name} › Shed › Garage › Shelf");
    }

    [Fact]
    public async Task DeletingANonEmptyLocation_Returns409WithCounts()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        var garage = await _client.CreateLocationAsync("Garage", home.Id, Ct);
        await _client.CreateLocationAsync("Shelf", garage.Id, Ct);
        await _client.CreateAssetAsync(new SaveAssetRequest("Drill", garage.Id, IsAgentAccessible: false), Ct);

        var response = await _client.DeleteAsync($"/api/locus/locations/{garage.Id}", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        problem.RootElement.GetProperty("childCount").GetInt32().Should().Be(1);
        problem.RootElement.GetProperty("assetCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task DeletingWithMoveContentsTo_ReparentsChildrenAndAssets()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        var garage = await _client.CreateLocationAsync("Garage", home.Id, Ct);
        var shelf = await _client.CreateLocationAsync("Shelf", garage.Id, Ct);
        var drill = await _client.CreateAssetAsync(new SaveAssetRequest("Drill", garage.Id, IsAgentAccessible: false), Ct);

        var response = await _client.DeleteAsync($"/api/locus/locations/{garage.Id}?moveContentsTo={home.Id}", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.GetAsync($"/api/locus/locations/{garage.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var movedShelf = await _client.GetFromJsonAsync<LocationDto>($"/api/locus/locations/{shelf.Id}", Ct);
        movedShelf!.ParentLocationId.Should().Be(home.Id);

        var movedDrill = await _client.GetFromJsonAsync<AssetDto>($"/api/locus/assets/{drill.Id}", Ct);
        movedDrill!.LocationId.Should().Be(home.Id);
    }

    [Fact]
    public async Task DeletingWithATargetInsideTheSubtree_Returns409()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        var garage = await _client.CreateLocationAsync("Garage", home.Id, Ct);
        var shelf = await _client.CreateLocationAsync("Shelf", garage.Id, Ct);

        var response = await _client.DeleteAsync($"/api/locus/locations/{garage.Id}?moveContentsTo={shelf.Id}", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeletingWithAMoveThatClashesWithASiblingName_Returns409()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        var garage = await _client.CreateLocationAsync("Garage", home.Id, Ct);
        await _client.CreateLocationAsync("Shelf", garage.Id, Ct);
        await _client.CreateLocationAsync("shelf", home.Id, Ct);

        var response = await _client.DeleteAsync($"/api/locus/locations/{garage.Id}?moveContentsTo={home.Id}", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RenamingALocation_QueuesKnowledgeSyncForSharedAssetsBelowIt()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        var garage = await _client.CreateLocationAsync("Garage", home.Id, Ct);
        var shared = await _client.CreateAssetAsync(new SaveAssetRequest("Shared drill", garage.Id), Ct);
        var hidden = await _client.CreateAssetAsync(new SaveAssetRequest("Hidden drill", garage.Id, IsAgentAccessible: false), Ct);

        int versionBefore;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LocusDbContext>();
            versionBefore = (await db.KnowledgeSyncEntries.SingleAsync(e => e.AssetId == shared.Id, Ct)).Version;
        }

        var response = await _client.PutAsJsonAsync($"/api/locus/locations/{home.Id}", new SaveLocationRequest(home.Name + "-renamed", null, null, null), Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LocusDbContext>();
            (await db.KnowledgeSyncEntries.SingleAsync(e => e.AssetId == shared.Id, Ct)).Version.Should().BeGreaterThan(versionBefore);
            (await db.KnowledgeSyncEntries.AnyAsync(e => e.AssetId == hidden.Id, Ct)).Should().BeFalse();
        }
    }
}
