using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace AgentRuntime.Infrastructure.Agents;

public sealed class CompositeAgentFactory(
    IAgentRegistry registry,
    IChatClient chatClient,
    IAgentToolProvider agentToolProvider,
    IAgentManager agentManager,
    IServiceProvider serviceProvider) : IAgentFactory
{
    public async Task<AIAgent> CreateAsync(string agentName, string? overrideInstructions = null, CancellationToken ct = default)
    {
        var definition = await ResolveDefinitionAsync(agentName, ct);
        if (definition is null)
        {
            throw new InvalidOperationException($"Agent '{agentName}' could not be resolved from Registry or Database.");
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

    private async Task<IAgent?> ResolveDefinitionAsync(string agentName, CancellationToken ct)
    {
        var type = registry.GetAgentType(agentName);
        if (type is not null)
        {
            return ActivatorUtilities.CreateInstance(serviceProvider, type) as IAgent;
        }

        var dbAgent = await agentManager.GetAgentByNameAsync(agentName, ct);

        if (dbAgent is not null)
        {
            return new DynamicCustomAgent(dbAgent.Name, dbAgent.Description);
        }

        return null;
    }
}
