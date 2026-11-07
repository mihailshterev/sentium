using Sentium.Praxis.Common.Helpers;
using Sentium.Praxis.Features.Learnings;
using Sentium.Praxis.Features.Registry;
using Sentium.Praxis.Features.Tools;
using Sentium.Praxis.Features.Skills;
using Sentium.Praxis.Features.Sentinel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text;
using Sentium.Praxis.Extensions;
using Sentium.Core.Constants;
using Sentium.Infrastructure.Inference;

namespace Sentium.Praxis.Features.Agents;

public sealed class CompositeAgentFactory(
    IOptions<InferenceOptions> inferenceOptions,
    IInferenceClientFactory inferenceClientFactory,
    IAgentToolProvider agentToolProvider,
    IAgentRepository agentRepository,
    IAgentRegistry agentRegistry,
    IRegistrySettingsService registrySettingsService,
    IAgentLearningService learningService,
    IServiceProvider serviceProvider,
    DynamicSkillsProvider dynamicSkillsProvider,
    SentinelClient sentinelClient,
    IPdpContextAccessor pdpContext,
    ILogger<CompositeAgentFactory> logger) : IAgentFactory, IDisposable
{
    private readonly ConcurrentDictionary<string, IChatClient> clientCache = new();

    public async Task<AIAgent> CreateAsync(string agentName, string? overrideInstructions = null, string? overrideModel = null, Guid? actingUserId = null, CancellationToken ct = default)
    {
        var definition = await ResolveDefinitionAsync(agentName, actingUserId, ct);
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
        var inferenceSettings = settings.Inference;

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

        var effectiveModel = !string.IsNullOrWhiteSpace(overrideModel) ? overrideModel : inferenceSettings?.DefaultModel ?? inferenceOptions.Value.DefaultModel;

        var harnessedClient = GetHarnessedClient(effectiveModel);

        var capabilityBlock = isOrchestrator ? string.Empty : await BuildCapabilityBlockAsync(tools, ct);

        string baseInstructions;
        if (overrideInstructions is not null)
        {
            baseInstructions = overrideInstructions;
        }
        else if (isOrchestrator)
        {
            var dbAgents = actingUserId is { } orchestratorUser ? await agentRepository.GetAgentsForUserAsync(orchestratorUser, ct) : await agentRepository.GetAgentsAsync(ct);
            baseInstructions = OrchestratorTemplate.Build(agentRegistry, dbAgents);
        }
        else
        {
            baseInstructions = definition.Instructions;
        }

        var instructions = string.IsNullOrEmpty(capabilityBlock) ? baseInstructions : $"{baseInstructions}\n\n{capabilityBlock}";

        // No per-request context size: it is fixed per model by the inference server's configuration.
        var chatOptions = new ChatOptions
        {
            Instructions = instructions,
            Tools = instrumentedTools,
            Temperature = inferenceSettings?.AgentTemperature ?? InferenceSettingsSnapshot.Default.AgentTemperature
        };

        var options = new ChatClientAgentOptions
        {
            Name = definition.Name,
            AIContextProviders = contextProviders,
            ChatOptions = chatOptions
        };

        return new ChatClientAgent(harnessedClient, options);
    }

    private async Task<IAgent?> ResolveDefinitionAsync(string agentName, Guid? actingUserId, CancellationToken ct)
    {
        var keyedAgent = serviceProvider.GetKeyedService<IAgent>(agentName);
        if (keyedAgent is not null)
        {
            return keyedAgent;
        }

        var dbAgent = actingUserId is { } uid ? await agentRepository.GetAgentByNameForUserAsync(agentName, uid, ct) : await agentRepository.GetAgentByNameAsync(agentName, ct);

        if (dbAgent is not null)
        {
            return new DynamicCustomAgent(dbAgent.Name, dbAgent.Description);
        }

        return null;
    }

    private IChatClient GetHarnessedClient(string? model)
    {
        var targetModel = !string.IsNullOrWhiteSpace(model) ? model : inferenceOptions.Value.DefaultModel;

        return clientCache.GetOrAdd(targetModel, m =>
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Building harnessed client for model: {Model}", m);
            }

            return inferenceClientFactory
                .CreateChatClient(PraxisModuleExtensions.LlmHttpClientName, m)
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
