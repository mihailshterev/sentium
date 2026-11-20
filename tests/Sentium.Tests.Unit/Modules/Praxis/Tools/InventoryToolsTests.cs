using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sentium.Locus.Contracts;
using Sentium.Locus.Contracts.Models;
using Sentium.Praxis.Features.Tools.Inventory;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Tools;

public sealed class InventoryToolsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly IInventoryLookup _inventory = Substitute.For<IInventoryLookup>();

    private static readonly InventoryAssetSummary Drill = new(Guid.NewGuid(), "Cordless drill", "Tools", ["power"], "Home › Garage › Shelf B");

    private static InventoryAssetDetail DrillDetail(string? notes = null)
    {
        return new(
            Drill.Id, Drill.Name, Drill.Category, Drill.Tags, Drill.LocationPath,
            "Bosch", "GSR 12V", "SN-1", null, "Charge before storing", new DateOnly(2024, 3, 1), null, null, notes,
            [new InventoryAttachmentInfo("manual.pdf", "Manual")]);
    }

    [Fact]
    public async Task FindAssets_BindsModelArguments_AndListsLocations()
    {
        _inventory.FindAssetsAsync(Arg.Any<InventoryAssetQuery>(), Arg.Any<CancellationToken>()).Returns([Drill]);
        var function = new FindAssetsTool(_inventory, Substitute.For<ILogger<FindAssetsTool>>()).CreateFunction();

        // Through the AIFunction, as the model calls it - including a quoted number.
        var result = (await function.InvokeAsync(Arguments("""{"query":"drill","location":"Garage","limit":"3"}"""), Ct))?.ToString();

        Assert.Contains("[Inventory - 1 item(s)]", result, StringComparison.Ordinal);
        Assert.Contains("Home › Garage › Shelf B", result, StringComparison.Ordinal);
        Assert.Contains(Drill.Id.ToString(), result, StringComparison.Ordinal);
        await _inventory.Received(1).FindAssetsAsync(
            Arg.Is<InventoryAssetQuery>(q => q.Query == "drill" && q.Location == "Garage" && q.Limit == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FindAssets_DefaultsTheLimit_AndTrimsTheQuery()
    {
        _inventory.FindAssetsAsync(Arg.Any<InventoryAssetQuery>(), Arg.Any<CancellationToken>()).Returns([]);
        var tool = new FindAssetsTool(_inventory, Substitute.For<ILogger<FindAssetsTool>>());

        var result = await tool.FindAsync("  hdmi cable ", ct: Ct);

        Assert.Contains("No matching items", result, StringComparison.Ordinal);
        await _inventory.Received(1).FindAssetsAsync(Arg.Is<InventoryAssetQuery>(q => q.Query == "hdmi cable" && q.Limit == 10), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FindAssets_ByTagOnly_SearchesWithoutAQuery()
    {
        _inventory.FindAssetsAsync(Arg.Any<InventoryAssetQuery>(), Arg.Any<CancellationToken>()).Returns([Drill]);
        var tool = new FindAssetsTool(_inventory, Substitute.For<ILogger<FindAssetsTool>>());

        await tool.FindAsync(" ", tag: "power", ct: Ct);

        await _inventory.Received(1).FindAssetsAsync(Arg.Is<InventoryAssetQuery>(q => q.Query == null && q.Tag == "power"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FindAssets_WithoutAnyCriteria_AsksForOne()
    {
        var tool = new FindAssetsTool(_inventory, Substitute.For<ILogger<FindAssetsTool>>());

        var result = await tool.FindAsync(string.Empty, ct: Ct);

        Assert.Contains("Provide a query", result, StringComparison.Ordinal);
        await _inventory.DidNotReceiveWithAnyArgs().FindAssetsAsync(default!, Ct);
    }

    [Fact]
    public async Task FindAssets_ReportsFailuresWithoutThrowing()
    {
        _inventory.FindAssetsAsync(Arg.Any<InventoryAssetQuery>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("db down"));
        var tool = new FindAssetsTool(_inventory, Substitute.For<ILogger<FindAssetsTool>>());

        var result = await tool.FindAsync("drill", ct: Ct);

        Assert.StartsWith("Error:", result, StringComparison.Ordinal);
        Assert.DoesNotContain("db down", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAsset_ById_ReturnsDetailWithFencedNotes()
    {
        _inventory.GetAssetAsync(Drill.Id, Arg.Any<CancellationToken>()).Returns(DrillDetail("Wear goggles."));
        var tool = new GetAssetTool(_inventory, Substitute.For<ILogger<GetAssetTool>>());

        var result = await tool.GetAsync(Drill.Id.ToString(), Ct);

        Assert.Contains("Cordless drill", result, StringComparison.Ordinal);
        Assert.Contains("Model: GSR 12V", result, StringComparison.Ordinal);
        Assert.Contains("Purchased: 2024-03-01", result, StringComparison.Ordinal);
        Assert.Contains("manual.pdf (manual)", result, StringComparison.Ordinal);
        Assert.Contains("not instructions", result, StringComparison.Ordinal);
        Assert.Contains("<<<\nWear goggles.\n>>>", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAsset_ByName_FallsBackToNameLookup()
    {
        _inventory.FindAssetsByNameAsync("cordless drill", Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([DrillDetail()]);
        var tool = new GetAssetTool(_inventory, Substitute.For<ILogger<GetAssetTool>>());

        var result = await tool.GetAsync("cordless drill", Ct);

        Assert.Contains("[Inventory item] Cordless drill", result, StringComparison.Ordinal);
        await _inventory.DidNotReceiveWithAnyArgs().GetAssetAsync(default, Ct);
    }

    [Fact]
    public async Task GetAsset_WithSeveralNameMatches_ListsThemForDisambiguation()
    {
        var other = DrillDetail() with { Id = Guid.NewGuid(), LocationPath = "Cabin" };
        _inventory.FindAssetsByNameAsync("drill", Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([DrillDetail(), other]);
        var tool = new GetAssetTool(_inventory, Substitute.For<ILogger<GetAssetTool>>());

        var result = await tool.GetAsync("drill", Ct);

        Assert.Contains("2 item(s)", result, StringComparison.Ordinal);
        Assert.Contains("Cabin", result, StringComparison.Ordinal);
        Assert.Contains("call get_asset again", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAsset_UnknownId_SaysSoWithoutRevealingPrivacy()
    {
        var tool = new GetAssetTool(_inventory, Substitute.For<ILogger<GetAssetTool>>());

        var result = await tool.GetAsync(Guid.NewGuid().ToString(), Ct);

        Assert.Equal("No shared household item has that id.", result);
    }

    [Fact]
    public async Task ListLocationContents_WithoutLocation_ListsRoots()
    {
        _inventory.GetLocationContentsAsync(null, false, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new InventoryLocationLookup(InventoryLocationLookupStatus.Found, null,
                [new InventoryLocationInfo(Guid.NewGuid(), "Home", "Home", 3)], [], [], false));
        var tool = new ListLocationContentsTool(_inventory, Substitute.For<ILogger<ListLocationContentsTool>>());

        var result = await tool.ListAsync(ct: Ct);

        Assert.Contains("1 top-level location(s)", result, StringComparison.Ordinal);
        Assert.Contains("- Home (3 sub-location(s))", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListLocationContents_Ambiguous_ListsCandidatePaths()
    {
        _inventory.GetLocationContentsAsync("Shelf", true, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new InventoryLocationLookup(InventoryLocationLookupStatus.Ambiguous, null, [], [],
                [new InventoryLocationInfo(Guid.NewGuid(), "Shelf", "Home › Garage › Shelf", 0), new InventoryLocationInfo(Guid.NewGuid(), "Shelf", "Home › Kitchen › Shelf", 0)], false));
        var tool = new ListLocationContentsTool(_inventory, Substitute.For<ILogger<ListLocationContentsTool>>());

        var result = (await tool.CreateFunction().InvokeAsync(Arguments("""{"location":"Shelf","includeNested":true}"""), Ct))?.ToString();

        Assert.Contains("Several locations match", result, StringComparison.Ordinal);
        Assert.Contains("Home › Garage › Shelf", result, StringComparison.Ordinal);
        Assert.Contains("Home › Kitchen › Shelf", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListLocationContents_Found_ListsSubLocationsAndItems()
    {
        var garage = new InventoryLocationInfo(Guid.NewGuid(), "Garage", "Home › Garage", 1);
        _inventory.GetLocationContentsAsync("Garage", false, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new InventoryLocationLookup(InventoryLocationLookupStatus.Found, garage,
                [new InventoryLocationInfo(Guid.NewGuid(), "Shelf B", "Home › Garage › Shelf B", 0)], [Drill], [], true));
        var tool = new ListLocationContentsTool(_inventory, Substitute.For<ILogger<ListLocationContentsTool>>());

        var result = await tool.ListAsync("Garage", ct: Ct);

        Assert.Contains("[Inventory location] Home › Garage", result, StringComparison.Ordinal);
        Assert.Contains("Home › Garage › Shelf B", result, StringComparison.Ordinal);
        Assert.Contains("Cordless drill", result, StringComparison.Ordinal);
        Assert.Contains("truncated", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Arguments as Microsoft.Extensions.AI hands them to a function: JSON elements parsed from the model's tool call.
    /// </summary>
    private static AIFunctionArguments Arguments(string json)
    {
        using var document = JsonDocument.Parse(json);
        return new AIFunctionArguments(document.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value.Clone()));
    }
}
