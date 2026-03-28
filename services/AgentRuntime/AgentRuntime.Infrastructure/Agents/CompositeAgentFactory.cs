using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentRuntime.Infrastructure.Agents;

public sealed class CompositeAgentFactory(
    IAgentRegistry registry,
    IChatClient chatClient,
    IAgentToolProvider agentToolProvider,
    IAgentManager agentManager) : IAgentFactory
{
    public async Task<AIAgent> CreateAsync(string agentName, string? overrideInstructions = null, CancellationToken ct = default)
    {
        IAgent? definition = null;

        var type = registry.GetAgentType(agentName);
        if (type is not null)
        {
            definition = Activator.CreateInstance(type) as IAgent;
        }
        else
        {
            var dbAgents = await agentManager.GetAgentsAsync(ct);
            var dbData = dbAgents.FirstOrDefault(a => a.Name.Equals(agentName, StringComparison.OrdinalIgnoreCase));

            if (dbData is not null)
            {
                definition = new DynamicCustomAgent(dbData.Name, dbData.Description);
            }
        }

        if (definition is null)
        {
            throw new InvalidOperationException($"Agent '{agentName}' could not be found in Registry or Database.");
        }

        return new ChatClientAgent(chatClient, new ChatClientAgentOptions
        {
            Name = definition.Name,
            ChatOptions = new ChatOptions
            {
                Instructions = overrideInstructions ?? definition.Instructions,
                Tools = agentToolProvider.GetToolsForAgent(definition.Name, ct)
            }
        });
    }
}
