using Microsoft.Extensions.AI;

namespace Sentium.Praxis.Features.Tools;

/// <summary>
/// Resolves the set of AI tools available to a named agent.
/// </summary>
internal interface IAgentToolProvider
{
    List<AITool> GetToolsForAgent(string agentName, CancellationToken ct);
}
