using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Core.Results;
using Sentium.Locus.Data;
using Sentium.Locus.Dtos;
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

        asset.DisplayName.Should().Be("Cordless drill");
        asset.Tags.Should().Equal("power tools", "diy");
        asset.PurchaseDate.Should().Be(new DateOnly(2024, 3, 1));
        asset.LocationPath.Should().Be(home.Name);
        asset.LocationCrumbs.Should().ContainSingle().Which.Id.Should().Be(home.Id);
        asset.KnowledgeStatus.Should().Be(AssetKnowledgeStatus.Pending);
        asset.CreatedBy.Should().Be("TestUser");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LocusDbContext>();
        (await db.KnowledgeSyncEntries.AnyAsync(e => e.AssetId == asset.Id, Ct)).Should().BeTrue();
    }

    [Fact]
    public async Task CreatePrivateAsset_IsNotShared_AndQueuesNothing()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);

        var asset = await _client.CreateAssetAsync(new SaveAssetRequest("Diary", home.Id, IsAgentAccessible: false), Ct);

        asset.KnowledgeStatus.Should().Be(AssetKnowledgeStatus.NotShared);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LocusDbContext>();
        (await db.KnowledgeSyncEntries.AnyAsync(e => e.AssetId == asset.Id, Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsset_InUnknownLocation_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/locus/assets", new SaveAssetRequest("Ghost", Guid.NewGuid()), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateAsset_WithoutName_Returns400()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);

        var response = await _client.PostAsJsonAsync("/api/locus/assets", new SaveAssetRequest(" ", home.Id), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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

        (await SearchAsync($"q=garage{token}+drill"))!.Items.Should().ContainSingle().Which.Id.Should().Be(drill.Id);
        (await SearchAsync($"q=bosch+{token}"))!.TotalCount.Should().Be(2);
        (await SearchAsync($"q=DRI+{token}"))!.Items.Should().ContainSingle().Which.LocationPath.Should().EndWith($"Garage{token} › Shelf");
    }

    [Fact]
    public async Task Search_TreatsLikeWildcardsLiterally()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        await _client.CreateAssetAsync(new SaveAssetRequest($"Cotton 100% {token}", home.Id), Ct);
        await _client.CreateAssetAsync(new SaveAssetRequest($"Cotton 1000 {token}", home.Id), Ct);

        var result = await SearchAsync($"q={Uri.EscapeDataString("100%")}&locationId={home.Id}");

        result!.Items.Should().ContainSingle().Which.DisplayName.Should().StartWith("Cotton 100%");
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

        (await SearchAsync($"locationId={home.Id}"))!.TotalCount.Should().Be(4);
        (await SearchAsync($"locationId={home.Id}&includeSubLocations=false"))!.TotalCount.Should().Be(1);
        (await SearchAsync($"locationId={home.Id}&tag=power"))!.TotalCount.Should().Be(3);
        (await SearchAsync($"locationId={home.Id}&tag=power&tag=Cordless"))!.Items.Should().ContainSingle().Which.DisplayName.Should().Be("Drill");
        (await SearchAsync($"locationId={home.Id}&category=Lighting"))!.Items.Should().ContainSingle().Which.DisplayName.Should().Be("Lamp");
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

        page2!.TotalCount.Should().Be(5);
        page2.TotalPages.Should().Be(3);
        page2.Items.Select(i => i.DisplayName).Should().Equal("C", "D");
    }

    [Fact]
    public async Task TagsAndCategories_AreSuggested()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        var tag = $"zz{Guid.NewGuid():N}"[..12];
        await _client.CreateAssetAsync(new SaveAssetRequest("Thing", home.Id, Category: "Gadgets", Tags: [tag]), Ct);

        var tags = await _client.GetFromJsonAsync<List<TagCountDto>>($"/api/locus/tags?prefix={tag}", Ct);
        var categories = await _client.GetFromJsonAsync<List<string>>("/api/locus/categories", Ct);

        tags.Should().ContainSingle().Which.Should().Be(new TagCountDto(tag, 1));
        categories.Should().Contain("Gadgets");
    }

    [Fact]
    public async Task MakingAnAssetPrivate_QueuesRemoval()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        var asset = await _client.CreateAssetAsync(new SaveAssetRequest("Router", home.Id), Ct);

        var response = await _client.PutAsJsonAsync($"/api/locus/assets/{asset.Id}", new SaveAssetRequest("Router", home.Id, IsAgentAccessible: false), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<AssetDto>(Ct);
        updated!.IsAgentAccessible.Should().BeFalse();
        updated.KnowledgeStatus.Should().Be(AssetKnowledgeStatus.Pending, "the knowledge-base copy still has to be removed");
    }

    [Fact]
    public async Task DeleteAsset_Returns204_ThenGetReturns404()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        var asset = await _client.CreateAssetAsync(new SaveAssetRequest("Old phone", home.Id), Ct);

        (await _client.DeleteAsync($"/api/locus/assets/{asset.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.GetAsync($"/api/locus/assets/{asset.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.DeleteAsync($"/api/locus/assets/{asset.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task KnowledgeSyncRetry_Returns202_OrNotFound()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        var asset = await _client.CreateAssetAsync(new SaveAssetRequest("TV", home.Id), Ct);

        (await _client.PostAsync($"/api/locus/assets/{asset.Id}/knowledge-sync", null, Ct)).StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await _client.PostAsync($"/api/locus/assets/{Guid.NewGuid()}/knowledge-sync", null, Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
