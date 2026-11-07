using System.ComponentModel;
using Microsoft.Extensions.AI;
using Sentium.Locus.Contracts;
using Sentium.Locus.Contracts.Models;
using Sentium.Praxis.Features.Tools.Attributes;

namespace Sentium.Praxis.Features.Tools.Inventory;

/// <summary>
/// Returns everything assistants may know about one household item.
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low, Resource = ToolResource.Inventory, Action = ToolAction.Read)]
public sealed class GetAssetTool(IInventoryLookup inventory, ILogger<GetAssetTool> logger) : IAgentTool
{
    private const int MaxNameMatches = 3;

    public AIFunction CreateFunction() => AgentToolFunction.Create(
        this,
        GetAsync,
        ToolNames.GetAsset,
        "Get the full details of one household inventory item: location, maker, model, serial number, " +
        "purchase and service dates, warranty, care instructions and attached files.");

    public async Task<string> GetAsync(
        [Description($"The item's id (from {ToolNames.FindAssets}) or its exact name.")] string asset,
        CancellationToken ct = default)
    {
        var reference = InventoryToolText.Normalize(asset);
        if (reference is null)
        {
            return "Provide the item's id or name.";
        }

        try
        {
            if (Guid.TryParse(reference, out var id))
            {
                var detail = await inventory.GetAssetAsync(id, ct);
                return detail is null
                    ? "No shared household item has that id."
                    : InventoryToolFormatter.FormatDetail(detail);
            }

            var matches = await inventory.FindAssetsByNameAsync(reference, MaxNameMatches + 1, ct);

            return matches.Count switch
            {
                0 => $"No shared household item is named '{reference}'. Try {ToolNames.FindAssets} to search.",
                1 => InventoryToolFormatter.FormatDetail(matches[0]),
                _ => InventoryToolFormatter.FormatSummaries(
                    [.. matches.Take(MaxNameMatches).Select(m => new InventoryAssetSummary(m.Id, m.Name, m.Category, m.Tags, m.LocationPath))],
                    $"match that name - call {ToolNames.GetAsset} again with one of the ids")
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Inventory lookup failed");
            return "Error: The household inventory could not be read right now.";
        }
    }
}
