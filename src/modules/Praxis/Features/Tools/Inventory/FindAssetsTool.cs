using System.ComponentModel;
using Microsoft.Extensions.AI;
using Sentium.Locus.Contracts;
using Sentium.Locus.Contracts.Models;
using Sentium.Praxis.Contracts;

namespace Sentium.Praxis.Features.Tools.Inventory;

/// <summary>
/// Searches the household inventory (assets shared with assistants) and answers "where is X?".
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low, Resource = ToolResource.Inventory, Action = ToolAction.Search)]
public sealed class FindAssetsTool(IInventoryLookup inventory, ILogger<FindAssetsTool> logger) : IAgentTool
{
    private const int DefaultLimit = 10;
    private const int MaxLimit = 25;

    public AIFunction CreateFunction()
    {
        return AgentToolFunction.Create(
            this,
            FindAsync,
            ToolNames.FindAssets,
            "Search the household inventory for physical items and where they are kept. Use it for questions like " +
            "'where is the drill?' or 'which chargers do we have?'. Returns each item's location path and id; " +
            $"call {ToolNames.GetAsset} with an id for full details.");
    }

    public async Task<string> FindAsync(
        [Description("Words describing the item (name, brand, model, tag or room), e.g. 'cordless drill'. May be empty when filtering by location, tag or category.")] string query,
        [Description("Only search in this place and below it: a location name or path such as 'Garage' or 'Home/Garage'.")] string? location = null,
        [Description("Only items with this tag, e.g. 'tools'.")] string? tag = null,
        [Description("Only items in this category, e.g. 'Electronics'.")] string? category = null,
        [Description("Maximum number of items to return (1-25, default 10).")] int? limit = null,
        CancellationToken ct = default)
    {
        var criteria = new InventoryAssetQuery(
            InventoryToolText.Normalize(query),
            InventoryToolText.Normalize(location),
            InventoryToolText.Normalize(tag),
            InventoryToolText.Normalize(category),
            Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit));

        if (criteria is { Query: null, Location: null, Tag: null, Category: null })
        {
            return "Provide a query (or a location, tag or category) to search the inventory.";
        }

        try
        {
            var results = await inventory.FindAssetsAsync(criteria, ct);

            if (results.Count == 0)
            {
                return criteria.Location is null
                    ? "No matching items were found in the household inventory."
                    : $"No matching items were found in the household inventory at '{criteria.Location}'. The location may not exist; call {ToolNames.ListLocationContents} to see the locations.";
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
