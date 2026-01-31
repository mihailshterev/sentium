using AgentRuntime.Core.Agents;
using AgentRuntime.Infrastructure.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OllamaSharp;

namespace AgentRuntime.Infrastructure.Ollama;

public sealed class OllamaAgentFactory : IAgentFactory
{
    private readonly IChatClient Client;
    private readonly IAgentRegistry AgentRegistry;

    public OllamaAgentFactory(IAgentRegistry registry, string model = "gemma3:1b")
    {
        AgentRegistry = registry;
        Client = new OllamaApiClient(new Uri("http://localhost:11434")) { SelectedModel = model };
    }

    public AIAgent Create(string agentName, string? overrideInstructions = null, CancellationToken ct = default)
    {
        IAgent? definition = null;
        var type = AgentRegistry.GetAgentType(agentName);

        if (type != null)
        {
            definition = Activator.CreateInstance(type) as IAgent;
        }

        return new ChatClientAgent(Client, new ChatClientAgentOptions
        {
            Name = agentName,
            ChatOptions = new ChatOptions
            {
                Instructions = overrideInstructions ?? definition?.Instructions,
                Tools = AgentToolProvider.GetToolsForAgent(agentName, ct)
            }
        });
    }
}
