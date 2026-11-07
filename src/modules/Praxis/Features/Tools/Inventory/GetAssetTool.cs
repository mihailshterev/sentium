using Microsoft.Extensions.Logging;
using Sentium.Locus.Contracts;
using Sentium.Locus.Contracts.Models;
using Sentium.Praxis.Features.Tools.Attributes;

namespace Sentium.Praxis.Features.Tools.Inventory;

/// <summary>
/// Returns everything assistants may know about one household item.
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low)]
public sealed class GetAssetTool(IInventoryLookup inventory, ILogger<GetAssetTool> logger) : IAgentTool
{
    public const string ToolName = "get_asset";

    private const int MaxNameMatches = 3;

    public string Name => ToolName;

    public string Description =>
        "Get the full details of one household inventory item: location, maker, model, serial number, " +
        "purchase and service dates, warranty, care instructions and attached files.";

    public IReadOnlyList<AgentToolParameter> Parameters { get; } =
    [
        new("asset", "The item's id (from find_assets) or its exact name."),
    ];

    public async Task<string> ExecuteAsync(string input, CancellationToken ct)
    {
        var reference = InventoryToolInput.Parse(input).GetString("asset", isPrimary: true);
        if (reference is null)
        {
            return "Provide the item's id or name.";
        }

        try
        {
            if (Guid.TryParse(reference, out var id))
            {
                var asset = await inventory.GetAssetAsync(id, ct);
                return asset is null
                    ? "No shared household item has that id."
                    : InventoryToolFormatter.FormatDetail(asset);
            }

            var matches = await inventory.FindAssetsByNameAsync(reference, MaxNameMatches + 1, ct);

            return matches.Count switch
            {
                0 => $"No shared household item is named '{reference}'. Try find_assets to search.",
                1 => InventoryToolFormatter.FormatDetail(matches[0]),
                _ => InventoryToolFormatter.FormatSummaries(
                    [.. matches.Take(MaxNameMatches).Select(m => new InventoryAssetSummary(m.Id, m.Name, m.Category, m.Tags, m.LocationPath))],
                    "match that name - call get_asset again with one of the ids")
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Inventory lookup failed");
            return "Error: The household inventory could not be read right now.";
        }
    }
}
