using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentRuntime.Infrastructure.Ollama;

public sealed class OllamaAgentFactory(
    IAgentRegistry registry,
    IChatClient chatClient,
    IAgentToolProvider agentToolProvider) : IAgentFactory
{
    public AIAgent Create(string agentName, string? overrideInstructions = null, CancellationToken ct = default)
    {
        IAgent? definition = null;
        var type = registry.GetAgentType(agentName);

        if (type != null)
        {
            definition = Activator.CreateInstance(type) as IAgent;
        }

        return new ChatClientAgent(chatClient, new ChatClientAgentOptions
        {
            Name = agentName,
            ChatOptions = new ChatOptions
            {
                Instructions = overrideInstructions ?? definition?.Instructions,
                Tools = agentToolProvider.GetToolsForAgent(agentName, ct)
            }
        });
    }
}
