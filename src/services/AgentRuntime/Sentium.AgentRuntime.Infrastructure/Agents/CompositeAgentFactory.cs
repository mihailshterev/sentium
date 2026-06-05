using Sentium.AgentRuntime.Application.Common.Helpers;
using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Learnings;
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
using OllamaSharp.Models;
using System.Collections.Concurrent;
using System.Text;
using Sentium.AgentRuntime.Infrastructure.Extensions;
using Sentium.Shared.Constants;

namespace Sentium.AgentRuntime.Infrastructure.Agents;

public sealed class CompositeAgentFactory(
    IChatClient defaultChatClient,
    OllamaOptions ollamaOptions,
    IAgentToolProvider agentToolProvider,
    IAgentRepository agentRepository,
    IAgentRegistry agentRegistry,
    IRegistrySettingsService registrySettingsService,
    IAgentLearningService learningService,
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

        var isOrchestrator = string.Equals(definition.Name, AgentRole.Orchestrator, StringComparison.OrdinalIgnoreCase);

        var settings = await registrySettingsService.GetAsync(actingUserId, ct);
        var ollamaSettings = settings.Ollama;

        var tools = isOrchestrator ? [] : agentToolProvider.GetToolsForAgent(definition.Name, ct);

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

        var contextProviders = new List<AIContextProvider>();
        if (!isOrchestrator)
        {
            contextProviders.Add(await dynamicSkillsProvider.BuildAsync(ct));
        }

        var effectiveModel = !string.IsNullOrWhiteSpace(overrideModel) ? overrideModel : ollamaSettings?.DefaultModel ?? ollamaOptions.DefaultModel;

        var harnessedClient = GetHarnessedClient(effectiveModel);

        var capabilityBlock = isOrchestrator ? string.Empty : await BuildCapabilityBlockAsync(tools, ct);

        string baseInstructions;
        if (overrideInstructions is not null)
        {
            baseInstructions = overrideInstructions;
        }
        else if (isOrchestrator)
        {
            var dbAgents = await agentRepository.GetAgentsAsync(ct);
            baseInstructions = OrchestratorTemplate.Build(agentRegistry, dbAgents);
        }
        else
        {
            baseInstructions = definition.Instructions;
        }

        var instructions = string.IsNullOrEmpty(capabilityBlock) ? baseInstructions : $"{baseInstructions}\n\n{capabilityBlock}";

        var chatOptions = new ChatOptions
        {
            Instructions = instructions,
            Tools = instrumentedTools,
            Temperature = ollamaSettings?.AgentTemperature ?? ollamaOptions.AgentTemperature
        };

        chatOptions.AddOllamaOption(OllamaOption.NumCtx, ollamaSettings?.AgentContextWindow ?? ollamaOptions.AgentContextWindow);

        var options = new ChatClientAgentOptions
        {
            Name = definition.Name,
            AIContextProviders = contextProviders,
            ChatOptions = chatOptions
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

        var dbAgent = await agentRepository.GetAgentByNameAsync(agentName, ct);

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

#pragma warning disable CA2000 // Dispose objects before losing scope
            var harnessedDefault = new HarnessedChatClient(defaultChatClient, registrySettingsService, pdpContext)
                .WithLearnings(learningService, pdpContext);
#pragma warning restore CA2000 // Dispose objects before losing scope
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
                .AsHarnessed(registrySettingsService, pdpContext)
                .WithLearnings(learningService, pdpContext);
        });
    }

    /// <summary>
    /// Builds the per-agent capability catalogue (### AVAILABLE TOOLS / ### AVAILABLE SKILLS) listing the
    /// exact names of the tools and skills this agent has, so smaller models stop inventing tool calls.
    /// </summary>
    private async Task<string> BuildCapabilityBlockAsync(IEnumerable<AITool> tools, CancellationToken ct)
    {
        var sb = new StringBuilder();

        var functions = tools.OfType<AIFunction>().ToList();
        if (functions.Count > 0)
        {
            sb.AppendLine("### AVAILABLE TOOLS");
            foreach (var function in functions)
            {
                sb.Append("- ").Append(function.Name).Append(": ").AppendLine(Summarize(function.Description));
            }
        }

        var skills = await dynamicSkillsProvider.GetCatalogAsync(ct);
        if (skills.Count > 0)
        {
            if (sb.Length > 0)
            {
                sb.AppendLine();
            }

            sb.AppendLine("### AVAILABLE SKILLS");
            sb.AppendLine("Unlock with load_skill before use.");
            foreach (var skill in skills)
            {
                sb.Append("- ").Append(skill.Name).Append(": ").AppendLine(Summarize(skill.Description));
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static string Summarize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var oneLine = text.ReplaceLineEndings(" ").Trim();
        return oneLine.Length > 160 ? string.Concat(oneLine.AsSpan(0, 160), "…") : oneLine;
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
