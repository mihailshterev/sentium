using Sentium.AgentRuntime.Application.Common.Helpers;
using Sentium.AgentRuntime.Core.Agents;

namespace Sentium.AgentRuntime.Application.Agents.Native;

/// <summary>
/// A specialized native agent responsible for task decomposition and agent orchestration.
/// The PlannerAgent analyzes incoming requests to select the most appropriate expert agents
/// for the given problem domain.
/// </summary>
public sealed class PlannerAgent : IAgent
{
    /// <inheritdoc />
    /// <value>"Planner Agent"</value>
    public string Name => "Planner Agent";

    /// <inheritdoc />
    /// <remarks>
    /// The available-agents section is injected at runtime by <see cref="CompositeAgentFactory"/>
    /// using <see cref="PlannerTemplate.Build(IAgentRegistry, System.Collections.Generic.IReadOnlyList{Sentium.AgentRuntime.Core.Dtos.AgentResponse})"/>,
    /// so these instructions never go stale regardless of which agents are registered.
    /// </remarks>
    public string Instructions => PlannerTemplate.SystemRole;
}
