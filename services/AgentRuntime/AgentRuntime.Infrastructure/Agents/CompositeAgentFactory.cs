using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Harness;
using AgentRuntime.Core.Tools;
using AgentRuntime.Infrastructure.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentRuntime.Infrastructure.Agents;

public sealed class CompositeAgentFactory(
    IAgentRegistry registry,
    IChatClient chatClient,
    IAgentToolProvider agentToolProvider,
    IAgentManager agentManager,
    IServiceProvider serviceProvider,
    ILogger<CompositeAgentFactory> logger) : IAgentFactory
{
    public async Task<AIAgent> CreateAsync(string agentName, string? overrideInstructions = null, CancellationToken ct = default)
    {
        var definition = await ResolveDefinitionAsync(agentName, ct);
        if (definition is null)
        {
            throw new InvalidOperationException($"Agent '{agentName}' could not be resolved from Registry or Database.");
        }

        var tools = agentToolProvider.GetToolsForAgent(definition.Name, ct);

        var instrumentedTools = tools
            .OfType<AIFunction>()
            .Select(func => new DiagnosticToolDecorator(func, logger))
            .Cast<AITool>()
            .ToList();

#pragma warning disable CA2000
        var harnessedClient = new HarnessedChatClient(chatClient, UniversalSystemHarness.Policy);
#pragma warning restore CA2000

        var options = new ChatClientAgentOptions
        {
            Name = definition.Name,
            ChatOptions = new ChatOptions
            {
                Instructions = overrideInstructions ?? definition.Instructions,
                Tools = instrumentedTools
            }
        };

        return new ChatClientAgent(harnessedClient, options);
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
