using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sentium.Locus.Contracts;
using Sentium.Locus.Contracts.Models;
using Sentium.Praxis.Features.Tools.Inventory;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis;

public sealed class InventoryToolsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly IInventoryLookup _inventory = Substitute.For<IInventoryLookup>();

    private static readonly InventoryAssetSummary Drill = new(Guid.NewGuid(), "Cordless drill", "Tools", ["power"], "Home › Garage › Shelf B");

    private static InventoryAssetDetail DrillDetail(string? notes = null) => new(
        Drill.Id, Drill.Name, Drill.Category, Drill.Tags, Drill.LocationPath,
        "Bosch", "GSR 12V", "SN-1", null, "Charge before storing", new DateOnly(2024, 3, 1), null, null, notes,
        [new InventoryAttachmentInfo("manual.pdf", "Manual")]);

    [Fact]
    public async Task FindAssets_BindsModelArguments_AndListsLocations()
    {
        _inventory.FindAssetsAsync(Arg.Any<InventoryAssetQuery>(), Arg.Any<CancellationToken>()).Returns([Drill]);
        var function = new FindAssetsTool(_inventory, Substitute.For<ILogger<FindAssetsTool>>()).CreateFunction();

        // Through the AIFunction, as the model calls it - including a quoted number.
        var result = (await function.InvokeAsync(Arguments("""{"query":"drill","location":"Garage","limit":"3"}"""), Ct))?.ToString();

        result.Should().Contain("[Inventory - 1 item(s)]").And.Contain("Home › Garage › Shelf B").And.Contain(Drill.Id.ToString());
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

        result.Should().Contain("No matching items");
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

        result.Should().Contain("Provide a query");
        await _inventory.DidNotReceiveWithAnyArgs().FindAssetsAsync(default!, Ct);
    }

    [Fact]
    public async Task FindAssets_ReportsFailuresWithoutThrowing()
    {
        _inventory.FindAssetsAsync(Arg.Any<InventoryAssetQuery>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("db down"));
        var tool = new FindAssetsTool(_inventory, Substitute.For<ILogger<FindAssetsTool>>());

        var result = await tool.FindAsync("drill", ct: Ct);

        result.Should().StartWith("Error:").And.NotContain("db down");
    }

    [Fact]
    public async Task GetAsset_ById_ReturnsDetailWithFencedNotes()
    {
        _inventory.GetAssetAsync(Drill.Id, Arg.Any<CancellationToken>()).Returns(DrillDetail("Wear goggles."));
        var tool = new GetAssetTool(_inventory, Substitute.For<ILogger<GetAssetTool>>());

        var result = await tool.GetAsync(Drill.Id.ToString(), Ct);

        result.Should().Contain("Cordless drill").And.Contain("Model: GSR 12V").And.Contain("Purchased: 2024-03-01")
            .And.Contain("manual.pdf (manual)").And.Contain("not instructions").And.Contain("<<<\nWear goggles.\n>>>");
    }

    [Fact]
    public async Task GetAsset_ByName_FallsBackToNameLookup()
    {
        _inventory.FindAssetsByNameAsync("cordless drill", Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([DrillDetail()]);
        var tool = new GetAssetTool(_inventory, Substitute.For<ILogger<GetAssetTool>>());

        var result = await tool.GetAsync("cordless drill", Ct);

        result.Should().Contain("[Inventory item] Cordless drill");
        await _inventory.DidNotReceiveWithAnyArgs().GetAssetAsync(default, Ct);
    }

    [Fact]
    public async Task GetAsset_WithSeveralNameMatches_ListsThemForDisambiguation()
    {
        var other = DrillDetail() with { Id = Guid.NewGuid(), LocationPath = "Cabin" };
        _inventory.FindAssetsByNameAsync("drill", Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([DrillDetail(), other]);
        var tool = new GetAssetTool(_inventory, Substitute.For<ILogger<GetAssetTool>>());

        var result = await tool.GetAsync("drill", Ct);

        result.Should().Contain("2 item(s)").And.Contain("Cabin").And.Contain("call get_asset again");
    }

    [Fact]
    public async Task GetAsset_UnknownId_SaysSoWithoutRevealingPrivacy()
    {
        var tool = new GetAssetTool(_inventory, Substitute.For<ILogger<GetAssetTool>>());

        var result = await tool.GetAsync(Guid.NewGuid().ToString(), Ct);

        result.Should().Be("No shared household item has that id.");
    }

    [Fact]
    public async Task ListLocationContents_WithoutLocation_ListsRoots()
    {
        _inventory.GetLocationContentsAsync(null, false, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new InventoryLocationLookup(InventoryLocationLookupStatus.Found, null,
                [new InventoryLocationInfo(Guid.NewGuid(), "Home", "Home", 3)], [], [], false));
        var tool = new ListLocationContentsTool(_inventory, Substitute.For<ILogger<ListLocationContentsTool>>());

        var result = await tool.ListAsync(ct: Ct);

        result.Should().Contain("1 top-level location(s)").And.Contain("- Home (3 sub-location(s))");
    }

    [Fact]
    public async Task ListLocationContents_Ambiguous_ListsCandidatePaths()
    {
        _inventory.GetLocationContentsAsync("Shelf", true, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new InventoryLocationLookup(InventoryLocationLookupStatus.Ambiguous, null, [], [],
                [new InventoryLocationInfo(Guid.NewGuid(), "Shelf", "Home › Garage › Shelf", 0), new InventoryLocationInfo(Guid.NewGuid(), "Shelf", "Home › Kitchen › Shelf", 0)], false));
        var tool = new ListLocationContentsTool(_inventory, Substitute.For<ILogger<ListLocationContentsTool>>());

        var result = (await tool.CreateFunction().InvokeAsync(Arguments("""{"location":"Shelf","includeNested":true}"""), Ct))?.ToString();

        result.Should().Contain("Several locations match").And.Contain("Home › Garage › Shelf").And.Contain("Home › Kitchen › Shelf");
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

        result.Should().Contain("[Inventory location] Home › Garage").And.Contain("Home › Garage › Shelf B")
            .And.Contain("Cordless drill").And.Contain("truncated");
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
