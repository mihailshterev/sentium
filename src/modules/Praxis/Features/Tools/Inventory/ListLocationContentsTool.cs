using System.ComponentModel;
using Microsoft.Extensions.AI;
using Sentium.Locus.Contracts;
using Sentium.Locus.Contracts.Models;
using Sentium.Praxis.Contracts;

namespace Sentium.Praxis.Features.Tools.Inventory;

/// <summary>
/// Lists what is kept at a household location, or the top-level locations.
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low, Resource = ToolResource.Inventory, Action = ToolAction.Read)]
internal sealed class ListLocationContentsTool(IInventoryLookup inventory, ILogger<ListLocationContentsTool> logger) : IAgentTool
{
    public AIFunction CreateFunction()
    {
        return AgentToolFunction.Create(
            this,
            ListAsync,
            ToolNames.ListLocationContents,
            "List the sub-locations and items kept at a place in the household (e.g. 'Garage' or 'Home/Kitchen'). " +
            "Call it without a location to see the top-level locations.");
    }

    public async Task<string> ListAsync(
        [Description("A location name or path such as 'Garage' or 'Home/Garage'. Leave empty for the top-level locations.")] string? location = null,
        [Description("Also list items stored in sub-locations.")] bool? includeNested = null,
        CancellationToken ct = default)
    {
        var path = InventoryToolText.Normalize(location);

        try
        {
            var lookup = await inventory.GetLocationContentsAsync(path, includeNested ?? false, ct: ct);

            return lookup.Status switch
            {
                InventoryLocationLookupStatus.NotFound => $"No household location matches '{path}'. Call {ToolNames.ListLocationContents} without a location to see the top-level locations.",
                InventoryLocationLookupStatus.Ambiguous => InventoryToolFormatter.FormatCandidates(lookup.Candidates),
                _ => lookup.Location is null && lookup.SubLocations.Count == 0
                    ? "The household inventory has no locations yet."
                    : InventoryToolFormatter.FormatLocation(lookup)
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Inventory location lookup failed");
            return "Error: The household inventory could not be read right now.";
        }
    }
}
