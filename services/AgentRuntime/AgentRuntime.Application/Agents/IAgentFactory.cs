using AgentRuntime.Core.Agents;
using Microsoft.Agents.AI;

namespace AgentRuntime.Application.Agents;

public interface IAgentFactory
{
    ChatClientAgent CreateAgent(AgentRole role);
}
