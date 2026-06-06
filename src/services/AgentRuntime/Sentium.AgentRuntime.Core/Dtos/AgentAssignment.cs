namespace Sentium.AgentRuntime.Core.Dtos;

/// <summary>
/// An Orchestrator-produced pipeline step: the agent to run and the concrete sub-task assigned to it.
/// <see cref="Task"/> may be empty when the Orchestrator named an agent without a specific instruction.
/// </summary>
public sealed record AgentAssignment(string Agent, string Task);
