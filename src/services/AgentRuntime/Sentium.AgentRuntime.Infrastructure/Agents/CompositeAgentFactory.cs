using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Registry;
using Sentium.AgentRuntime.Core.Tools;
using Sentium.AgentRuntime.Infrastructure.Tools;
using Sentium.AgentRuntime.Infrastructure.Skills;
using Sentium.AgentRuntime.Infrastructure.Sentinel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using System.Collections.Concurrent;
using Sentium.AgentRuntime.Infrastructure.Extensions;
using Sentium.Shared.Constants;

namespace Sentium.AgentRuntime.Infrastructure.Agents;

public sealed class CompositeAgentFactory(
    IChatClient defaultChatClient,
    OllamaOptions ollamaOptions,
    IAgentToolProvider agentToolProvider,
    IAgentManager agentManager,
    IRegistrySettingsService registrySettingsService,
    IServiceProvider serviceProvider,
    IHttpClientFactory httpClientFactory,
    DynamicSkillsProvider dynamicSkillsProvider,
    SentinelClient sentinelClient,
    IPdpContextAccessor pdpContext,
    ILogger<CompositeAgentFactory> logger) : IAgentFactory, IDisposable
{
    private readonly ConcurrentDictionary<string, IChatClient> clientCache = new();
    private bool defaultInitialized;
    private readonly Lock syncLock = new();

    public async Task<AIAgent> CreateAsync(string agentName, string? overrideInstructions = null, string? overrideModel = null, Guid? actingUserId = null, CancellationToken ct = default)
    {
        EnsureDefaultIsHarnessed();

        var definition = await ResolveDefinitionAsync(agentName, ct);
        if (definition is null)
        {
            throw new InvalidOperationException($"Agent '{agentName}' could not be resolved.");
        }

        pdpContext.AgentName = definition.Name;

        if (actingUserId is { } uid)
        {
            pdpContext.UserId = uid;
        }

        var tools = agentToolProvider.GetToolsForAgent(definition.Name, ct);

        var instrumentedTools = tools
            .OfType<AIFunction>()
            .Select(func =>
            {
                AIFunction current = new DiagnosticToolDecorator(func, logger);

                current = new SentinelGuardedAIFunction(current, sentinelClient, pdpContext, definition.Name, logger);

                if (func is ApprovalRequiredAIFunction)
                {
                    return new ApprovalRequiredAIFunction(current);
                }

                return current;
            })
            .Cast<AITool>()
            .ToList();

        var skillsProvider = await dynamicSkillsProvider.BuildAsync(ct);

        var harnessedClient = GetHarnessedClient(overrideModel);

        var options = new ChatClientAgentOptions
        {
            Name = definition.Name,
            AIContextProviders = [skillsProvider],
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
        var keyedAgent = serviceProvider.GetKeyedService<IAgent>(agentName);
        if (keyedAgent is not null)
        {
            return keyedAgent;
        }

        var dbAgent = await agentManager.GetAgentByNameAsync(agentName, ct);

        if (dbAgent is not null)
        {
            return new DynamicCustomAgent(dbAgent.Name, dbAgent.Description);
        }

        return null;
    }

    private void EnsureDefaultIsHarnessed()
    {
        if (defaultInitialized)
        {
            return;
        }

        lock (syncLock)
        {
            if (defaultInitialized)
            {
                return;
            }

            var harnessedDefault = new HarnessedChatClient(defaultChatClient, registrySettingsService);
            clientCache.TryAdd(ollamaOptions.DefaultModel, harnessedDefault);
            defaultInitialized = true;
        }
    }

    private IChatClient GetHarnessedClient(string? model)
    {
        var targetModel = !string.IsNullOrWhiteSpace(model) ? model : ollamaOptions.DefaultModel;

        return clientCache.GetOrAdd(targetModel, m =>
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Building harnessed client for model: {Model}", m);
            }

            var httpClient = httpClientFactory.CreateClient(ResourceNames.Ollama);
            var ollamaClient = new OllamaApiClient(httpClient)
            {
                SelectedModel = m
            };

            return ollamaClient
                .AddSentiumPipeline()
                .AsHarnessed(registrySettingsService);
        });
    }

    public void Dispose()
    {
        foreach (var client in clientCache.Values)
        {
            if (client is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        clientCache.Clear();
    }
}
