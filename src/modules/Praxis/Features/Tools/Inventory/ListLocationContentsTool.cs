using Microsoft.Extensions.Logging;
using Sentium.Locus.Contracts;
using Sentium.Locus.Contracts.Models;
using Sentium.Praxis.Features.Tools.Attributes;

namespace Sentium.Praxis.Features.Tools.Inventory;

/// <summary>
/// Lists what is kept at a household location, or the top-level locations.
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low)]
public sealed class ListLocationContentsTool(IInventoryLookup inventory, ILogger<ListLocationContentsTool> logger) : IAgentTool
{
    public const string ToolName = "list_location_contents";

    public string Name => ToolName;

    public string Description =>
        "List the sub-locations and items kept at a place in the household (e.g. 'Garage' or 'Home/Kitchen'). " +
        "Call it without a location to see the top-level locations.";

    public IReadOnlyList<AgentToolParameter> Parameters { get; } =
    [
        new("location", "A location name or path such as 'Garage' or 'Home/Garage'. Leave empty for the top-level locations.", Required: false),
        new("includeNested", "Also list items stored in sub-locations.", AgentToolParameterType.Boolean, Required: false),
    ];

    public async Task<string> ExecuteAsync(string input, CancellationToken ct)
    {
        var args = InventoryToolInput.Parse(input);
        var location = args.GetString("location", isPrimary: true);
        var includeNested = args.GetBool("includeNested") ?? false;

        try
        {
            var lookup = await inventory.GetLocationContentsAsync(location, includeNested, ct: ct);

            return lookup.Status switch
            {
                InventoryLocationLookupStatus.NotFound => $"No household location matches '{location}'. Call list_location_contents without a location to see the top-level locations.",
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
