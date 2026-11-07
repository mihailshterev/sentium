using Microsoft.Extensions.Logging;
using Sentium.Locus.Contracts;
using Sentium.Locus.Contracts.Models;
using Sentium.Praxis.Features.Tools.Attributes;

namespace Sentium.Praxis.Features.Tools.Inventory;

/// <summary>
/// Searches the household inventory (assets shared with assistants) and answers "where is X?".
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low)]
public sealed class FindAssetsTool(IInventoryLookup inventory, ILogger<FindAssetsTool> logger) : IAgentTool
{
    public const string ToolName = "find_assets";

    public string Name => ToolName;

    public string Description =>
        "Search the household inventory for physical items and where they are kept. Use it for questions like " +
        "'where is the drill?' or 'which chargers do we have?'. Returns each item's location path and id; " +
        "call get_asset with an id for full details.";

    public IReadOnlyList<AgentToolParameter> Parameters { get; } =
    [
        new("query", "Words describing the item (name, brand, model, tag or room), e.g. 'cordless drill'."),
        new("location", "Only search in this place and below it: a location name or path such as 'Garage' or 'Home/Garage'.", Required: false),
        new("tag", "Only items with this tag, e.g. 'tools'.", Required: false),
        new("category", "Only items in this category, e.g. 'Electronics'.", Required: false),
        new("limit", "Maximum number of items to return (1-25, default 10).", AgentToolParameterType.Integer, Required: false),
    ];

    public async Task<string> ExecuteAsync(string input, CancellationToken ct)
    {
        var args = InventoryToolInput.Parse(input);
        var query = args.GetString("query", isPrimary: true);
        var location = args.GetString("location");
        var tag = args.GetString("tag");
        var category = args.GetString("category");

        if (query is null && location is null && tag is null && category is null)
        {
            return "Provide a query (or a location, tag or category) to search the inventory.";
        }

        try
        {
            var limit = Math.Clamp(args.GetInt("limit") ?? 10, 1, 25);
            var results = await inventory.FindAssetsAsync(new InventoryAssetQuery(query, location, tag, category, limit), ct);

            if (results.Count == 0)
            {
                return location is null
                    ? "No matching items were found in the household inventory."
                    : $"No matching items were found in the household inventory at '{location}'. The location may not exist; call list_location_contents to see the locations.";
            }

            return InventoryToolFormatter.FormatSummaries(results);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Inventory search failed");
            return "Error: The household inventory could not be searched right now.";
        }
    }
}
