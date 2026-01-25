namespace AgentRuntime.Core.Agents;

public interface IAgentRuntimeFactory
{
    IAgentRuntime Create(AgentRole role);
}
