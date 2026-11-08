using System.Collections.Frozen;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Dtos;
using Sentium.Praxis.Features.Mcp;

namespace Sentium.Praxis.Features.Tools;

/// <summary>
/// Groups the registered tools by the agents allowed to use them (<see cref="Attributes.AgentToolPolicyAttribute.AllowedAgents"/>)
/// and describes them for the tool picker. The catalogue also lists the approved MCP tools (read live from
/// <see cref="IMcpToolCatalog"/>, which changes at runtime); <see cref="GetToolsForAgent"/> stays built-in only, because MCP
/// tools are opt-in and selected by name (see <c>CompositeAgentFactory</c>).
/// </summary>
public sealed class AgentToolProvider : IAgentToolProvider, IToolCatalog
{
    private const string AllAgents = "*";

    private readonly FrozenDictionary<string, List<AITool>> toolCache;
    private readonly IReadOnlyList<ToolDescriptor> catalog;
    private readonly FrozenSet<string> names;
    private readonly IMcpToolCatalog? mcpTools;

    public AgentToolProvider(IEnumerable<IAgentTool> tools, IMcpToolCatalog? mcpTools = null)
    {
        ArgumentNullException.ThrowIfNull(tools);

        this.mcpTools = mcpTools;

        var map = new Dictionary<string, List<AITool>>(StringComparer.OrdinalIgnoreCase);
        var descriptors = new List<ToolDescriptor>();

        foreach (var tool in tools)
        {
            var function = tool.CreateFunction();
            var policy = ToolPolicyReader.GetPolicy(tool);

            descriptors.Add(new ToolDescriptor(
                function.Name,
                function.Description,
                policy.Resource.ToString(),
                policy.Action.ToString(),
                policy.RiskLevel.ToString(),
                policy.RequiresApproval));

            var keys = policy.AllowedAgents.Length == 0 ? [AllAgents] : policy.AllowedAgents;
            foreach (var key in keys)
            {
                if (!map.TryGetValue(key, out var list))
                {
                    map[key] = list = [];
                }

                list.Add(function);
            }
        }

        toolCache = map.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        catalog = [.. descriptors
            .DistinctBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(d => d.Resource, StringComparer.Ordinal)
            .ThenBy(d => d.Name, StringComparer.Ordinal)];
        names = catalog.Select(d => d.Name).ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<ToolDescriptor> GetTools() =>
        mcpTools?.Descriptors is { Count: > 0 } mcp ? [.. catalog, .. mcp] : catalog;

    public bool Contains(string name) => name is not null && (names.Contains(name) || (mcpTools?.Contains(name) ?? false));

    public List<AITool> GetToolsForAgent(string agentName, CancellationToken ct)
    {
        var result = new List<AITool>();

        if (toolCache.TryGetValue(AllAgents, out var globalTools))
        {
            result.AddRange(globalTools);
        }

        if (toolCache.TryGetValue(agentName, out var specificTools))
        {
            result.AddRange(specificTools);
        }

        return result;
    }
}
