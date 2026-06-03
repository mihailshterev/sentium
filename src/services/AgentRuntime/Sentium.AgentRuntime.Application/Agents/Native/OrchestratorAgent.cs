using Sentium.AgentRuntime.Application.Common.Helpers;
using Sentium.AgentRuntime.Core.Agents;

namespace Sentium.AgentRuntime.Application.Agents.Native;

/// <summary>
/// A specialized native agent responsible for task decomposition and agent orchestration.
/// The OrchestratorAgent analyzes incoming requests, selects the most appropriate expert agents,
/// and assigns each one a concrete sub-task so the squad divides the work.
/// </summary>
public sealed class OrchestratorAgent : IAgent
{
    /// <inheritdoc />
    /// <value><see cref="AgentRole.Orchestrator"/> ("Orchestrator")</value>
    /// <remarks>Must equal the key this agent is registered under (<see cref="AgentRole.Orchestrator"/>); the factory
    /// resolves agents by that key and special-cases the orchestrator by name, so any drift silently breaks planning.</remarks>
    public string Name => AgentRole.Orchestrator;

    /// <inheritdoc />
    /// <remarks>
    /// The available-agents section is injected at runtime by <see cref="CompositeAgentFactory"/>
    /// using <see cref="OrchestratorTemplate.Build(IAgentRegistry, System.Collections.Generic.IReadOnlyList{Sentium.AgentRuntime.Core.Dtos.AgentResponse})"/>,
    /// so these instructions never go stale regardless of which agents are registered.
    /// </remarks>
    public string Instructions => OrchestratorTemplate.SystemRole;
}
