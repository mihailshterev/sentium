using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Core.Results;
using Sentium.Locus.Data;
using Sentium.Locus.Features.Assets;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Locus;

public sealed class LocusAssetApiTests(LocusApiFactory factory) : IClassFixture<LocusApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<PagedResponse<AssetSummaryDto>?> SearchAsync(string query)
        => _client.GetFromJsonAsync<PagedResponse<AssetSummaryDto>>($"/api/locus/assets?{query}", Ct);

    [Fact]
    public async Task CreateAsset_NormalizesTags_AndQueuesKnowledgeSync()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);

        var asset = await _client.CreateAssetAsync(new SaveAssetRequest("  Cordless drill ", home.Id, Category: "Tools",
            PurchaseDate: new DateOnly(2024, 3, 1), Tags: [" Power  Tools", "power tools", "DIY"]), Ct);

        Assert.Equal("Cordless drill", asset.DisplayName);
        Assert.Equal(new string[] { "power tools", "diy" }, asset.Tags);
        Assert.Equal(new DateOnly(2024, 3, 1), asset.PurchaseDate);
        Assert.Equal(home.Name, asset.LocationPath);
        Assert.Equal(home.Id, Assert.Single(asset.LocationCrumbs).Id);
        Assert.Equal(AssetKnowledgeStatus.Pending, asset.KnowledgeStatus);
        Assert.Equal("TestUser", asset.CreatedBy);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LocusDbContext>();
        Assert.True(await db.KnowledgeSyncEntries.AnyAsync(e => e.AssetId == asset.Id, Ct));
    }

    [Fact]
    public async Task CreatePrivateAsset_IsNotShared_AndQueuesNothing()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);

        var asset = await _client.CreateAssetAsync(new SaveAssetRequest("Diary", home.Id, IsAgentAccessible: false), Ct);

        Assert.Equal(AssetKnowledgeStatus.NotShared, asset.KnowledgeStatus);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LocusDbContext>();
        Assert.False(await db.KnowledgeSyncEntries.AnyAsync(e => e.AssetId == asset.Id, Ct));
    }

    [Fact]
    public async Task CreateAsset_InUnknownLocation_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/locus/assets", new SaveAssetRequest("Ghost", Guid.NewGuid()), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateAsset_WithoutName_Returns400()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);

        var response = await _client.PostAsJsonAsync("/api/locus/assets", new SaveAssetRequest(" ", home.Id), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_MatchesFieldsAndContainingLocationNames()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        var garage = await _client.CreateLocationAsync($"Garage{token}", home.Id, Ct);
        var shelf = await _client.CreateLocationAsync("Shelf", garage.Id, Ct);
        var kitchen = await _client.CreateLocationAsync("Kitchen", home.Id, Ct);

        var drill = await _client.CreateAssetAsync(new SaveAssetRequest($"Drill {token}", shelf.Id, Manufacturer: "Bosch"), Ct);
        await _client.CreateAssetAsync(new SaveAssetRequest($"Kettle {token}", kitchen.Id, Manufacturer: "Bosch"), Ct);

        Assert.Equal(drill.Id, Assert.Single((await SearchAsync($"q=garage{token}+drill"))!.Items).Id);
        Assert.Equal(2, (await SearchAsync($"q=bosch+{token}"))!.TotalCount);
        Assert.EndsWith($"Garage{token} › Shelf", Assert.Single((await SearchAsync($"q=DRI+{token}"))!.Items).LocationPath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Search_TreatsLikeWildcardsLiterally()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        await _client.CreateAssetAsync(new SaveAssetRequest($"Cotton 100% {token}", home.Id), Ct);
        await _client.CreateAssetAsync(new SaveAssetRequest($"Cotton 1000 {token}", home.Id), Ct);

        var result = await SearchAsync($"q={Uri.EscapeDataString("100%")}&locationId={home.Id}");

        Assert.StartsWith("Cotton 100%", Assert.Single(result!.Items).DisplayName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Search_FiltersByLocationSubtreeTagsAndCategory()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        var garage = await _client.CreateLocationAsync("Garage", home.Id, Ct);

        await _client.CreateAssetAsync(new SaveAssetRequest("Hammer", home.Id, Category: "Tools", Tags: ["hand"]), Ct);
        await _client.CreateAssetAsync(new SaveAssetRequest("Drill", garage.Id, Category: "Tools", Tags: ["power", "cordless"]), Ct);
        await _client.CreateAssetAsync(new SaveAssetRequest("Saw", garage.Id, Category: "Tools", Tags: ["power"]), Ct);
        await _client.CreateAssetAsync(new SaveAssetRequest("Lamp", garage.Id, Category: "Lighting", Tags: ["power"]), Ct);

        Assert.Equal(4, (await SearchAsync($"locationId={home.Id}"))!.TotalCount);
        Assert.Equal(1, (await SearchAsync($"locationId={home.Id}&includeSubLocations=false"))!.TotalCount);
        Assert.Equal(3, (await SearchAsync($"locationId={home.Id}&tag=power"))!.TotalCount);
        Assert.Equal("Drill", Assert.Single((await SearchAsync($"locationId={home.Id}&tag=power&tag=Cordless"))!.Items).DisplayName);
        Assert.Equal("Lamp", Assert.Single((await SearchAsync($"locationId={home.Id}&category=Lighting"))!.Items).DisplayName);
    }

    [Fact]
    public async Task Search_PagesWithStableTotals()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        foreach (var name in new[] { "A", "B", "C", "D", "E" })
        {
            await _client.CreateAssetAsync(new SaveAssetRequest(name, home.Id), Ct);
        }

        var page2 = await SearchAsync($"locationId={home.Id}&page=2&pageSize=2");

        Assert.Equal(5, page2!.TotalCount);
        Assert.Equal(3, page2.TotalPages);
        Assert.Equal(new string[] { "C", "D" }, page2.Items.Select(i => i.DisplayName));
    }

    [Fact]
    public async Task TagsAndCategories_AreSuggested()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        var tag = $"zz{Guid.NewGuid():N}"[..12];
        await _client.CreateAssetAsync(new SaveAssetRequest("Thing", home.Id, Category: "Gadgets", Tags: [tag]), Ct);

        var tags = await _client.GetFromJsonAsync<List<TagCountDto>>($"/api/locus/tags?prefix={tag}", Ct);
        var categories = await _client.GetFromJsonAsync<List<string>>("/api/locus/categories", Ct);

        Assert.Equal(new TagCountDto(tag, 1), Assert.Single(tags!));
        Assert.Contains("Gadgets", categories!);
    }

    [Fact]
    public async Task MakingAnAssetPrivate_QueuesRemoval()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        var asset = await _client.CreateAssetAsync(new SaveAssetRequest("Router", home.Id), Ct);

        var response = await _client.PutAsJsonAsync($"/api/locus/assets/{asset.Id}", new SaveAssetRequest("Router", home.Id, IsAgentAccessible: false), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<AssetDto>(Ct);
        Assert.False(updated!.IsAgentAccessible);
        // the knowledge-base copy still has to be removed
        Assert.Equal(AssetKnowledgeStatus.Pending, updated.KnowledgeStatus);
    }

    [Fact]
    public async Task DeleteAsset_Returns204_ThenGetReturns404()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        var asset = await _client.CreateAssetAsync(new SaveAssetRequest("Old phone", home.Id), Ct);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/locus/assets/{asset.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/locus/assets/{asset.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync($"/api/locus/assets/{asset.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task KnowledgeSyncRetry_Returns202_OrNotFound()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        var asset = await _client.CreateAssetAsync(new SaveAssetRequest("TV", home.Id), Ct);

        Assert.Equal(HttpStatusCode.Accepted, (await _client.PostAsync($"/api/locus/assets/{asset.Id}/knowledge-sync", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsync($"/api/locus/assets/{Guid.NewGuid()}/knowledge-sync", null, Ct)).StatusCode);
    }
}
