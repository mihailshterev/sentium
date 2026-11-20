using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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

        Assert.Equal($"{home.Name} › Garage", garage.Path);
        Assert.Equal(2, garage.Depth);
        Assert.Equal(home.Id, garage.ParentLocationId);
        Assert.Equal("TestUser", garage.UpdatedBy);

        var all = await _client.GetFromJsonAsync<List<LocationDto>>("/api/locus/locations", Ct);
        Assert.Equal(1, all!.Single(l => l.Id == home.Id).ChildCount);
    }

    [Fact]
    public async Task SiblingNames_AreUniqueCaseInsensitively()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        await _client.CreateLocationAsync("Kitchen", home.Id, Ct);

        var duplicate = await _client.PostAsJsonAsync("/api/locus/locations", new SaveLocationRequest("  KITCHEN ", null, null, home.Id), Ct);

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task RootNames_AreUniqueToo()
    {
        var name = LocusApi.Unique("Root");
        await _client.CreateLocationAsync(name, ct: Ct);

        var duplicate = await _client.PostAsJsonAsync("/api/locus/locations", new SaveLocationRequest(name.ToUpperInvariant(), null, null, null), Ct);

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task SameName_UnderDifferentParents_IsAllowed()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        var cabin = await _client.CreateLocationAsync(LocusApi.Unique("Cabin"), ct: Ct);

        await _client.CreateLocationAsync("Kitchen", home.Id, Ct);
        var second = await _client.PostAsJsonAsync("/api/locus/locations", new SaveLocationRequest("Kitchen", null, null, cabin.Id), Ct);

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Fact]
    public async Task UnknownParent_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/locus/locations", new SaveLocationRequest("Orphan", null, null, Guid.NewGuid()), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MovingALocationIntoItsOwnSubtree_Returns409()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        var garage = await _client.CreateLocationAsync("Garage", home.Id, Ct);
        var shelf = await _client.CreateLocationAsync("Shelf", garage.Id, Ct);

        var response = await _client.PutAsJsonAsync($"/api/locus/locations/{home.Id}", new SaveLocationRequest(home.Name, null, null, shelf.Id), Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task MovingALocation_UpdatesTheWholeSubtreePath()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        var shed = await _client.CreateLocationAsync("Shed", home.Id, Ct);
        var garage = await _client.CreateLocationAsync("Garage", home.Id, Ct);
        var shelf = await _client.CreateLocationAsync("Shelf", garage.Id, Ct);

        var response = await _client.PutAsJsonAsync($"/api/locus/locations/{garage.Id}", new SaveLocationRequest("Garage", null, null, shed.Id), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var moved = await _client.GetFromJsonAsync<LocationDto>($"/api/locus/locations/{shelf.Id}", Ct);
        Assert.Equal($"{home.Name} › Shed › Garage › Shelf", moved!.Path);
    }

    [Fact]
    public async Task DeletingANonEmptyLocation_Returns409WithCounts()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        var garage = await _client.CreateLocationAsync("Garage", home.Id, Ct);
        await _client.CreateLocationAsync("Shelf", garage.Id, Ct);
        await _client.CreateAssetAsync(new SaveAssetRequest("Drill", garage.Id, IsAgentAccessible: false), Ct);

        var response = await _client.DeleteAsync($"/api/locus/locations/{garage.Id}", Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(1, problem.RootElement.GetProperty("childCount").GetInt32());
        Assert.Equal(1, problem.RootElement.GetProperty("assetCount").GetInt32());
    }

    [Fact]
    public async Task DeletingWithMoveContentsTo_ReparentsChildrenAndAssets()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        var garage = await _client.CreateLocationAsync("Garage", home.Id, Ct);
        var shelf = await _client.CreateLocationAsync("Shelf", garage.Id, Ct);
        var drill = await _client.CreateAssetAsync(new SaveAssetRequest("Drill", garage.Id, IsAgentAccessible: false), Ct);

        var response = await _client.DeleteAsync($"/api/locus/locations/{garage.Id}?moveContentsTo={home.Id}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/locus/locations/{garage.Id}", Ct)).StatusCode);

        var movedShelf = await _client.GetFromJsonAsync<LocationDto>($"/api/locus/locations/{shelf.Id}", Ct);
        Assert.Equal(home.Id, movedShelf!.ParentLocationId);

        var movedDrill = await _client.GetFromJsonAsync<AssetDto>($"/api/locus/assets/{drill.Id}", Ct);
        Assert.Equal(home.Id, movedDrill!.LocationId);
    }

    [Fact]
    public async Task DeletingWithATargetInsideTheSubtree_Returns409()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        var garage = await _client.CreateLocationAsync("Garage", home.Id, Ct);
        var shelf = await _client.CreateLocationAsync("Shelf", garage.Id, Ct);

        var response = await _client.DeleteAsync($"/api/locus/locations/{garage.Id}?moveContentsTo={shelf.Id}", Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DeletingWithAMoveThatClashesWithASiblingName_Returns409()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        var garage = await _client.CreateLocationAsync("Garage", home.Id, Ct);
        await _client.CreateLocationAsync("Shelf", garage.Id, Ct);
        await _client.CreateLocationAsync("shelf", home.Id, Ct);

        var response = await _client.DeleteAsync($"/api/locus/locations/{garage.Id}?moveContentsTo={home.Id}", Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
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
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LocusDbContext>();
            Assert.True((await db.KnowledgeSyncEntries.SingleAsync(e => e.AssetId == shared.Id, Ct)).Version > versionBefore);
            Assert.False(await db.KnowledgeSyncEntries.AnyAsync(e => e.AssetId == hidden.Id, Ct));
        }
    }
}
