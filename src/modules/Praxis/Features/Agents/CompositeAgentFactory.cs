using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OllamaSharp.Models;
using Sentium.Praxis.Features.Learnings;
using Sentium.Praxis.Features.Registry;
using Sentium.Praxis.Features.Sentinel;
using Sentium.Praxis.Features.Skills;
using Sentium.Praxis.Features.Tools;

namespace Sentium.Praxis.Features.Agents;

/// <summary>
/// Builds a <see cref="ChatClientAgent"/> per request on the shared agent base client, adding per-agent function
/// invocation and context-window compaction (see <see cref="AgentChatPipeline"/>). Everything else that varies per agent
/// is expressed through native Agent Framework / Microsoft.Extensions.AI options: the model via
/// <see cref="ChatOptions.ModelId"/>, the composed instructions via <see cref="ChatOptions.Instructions"/> (one system
/// message), skills / learnings via <see cref="ChatClientAgentOptions.AIContextProviders"/>, and the Sentinel tool guard
/// as function-calling middleware.
/// </summary>
public sealed class CompositeAgentFactory(
    [FromKeyedServices(PraxisModuleExtensions.AgentChatClientKey)] IChatClient baseChatClient,
    OllamaOptions ollamaOptions,
    IAgentToolProvider agentToolProvider,
    IAgentRepository agentRepository,
    IRegistrySettingsService registrySettingsService,
    AgentSkillsProviderFactory skillsProviderFactory,
    LearningsContextProvider learningsProvider,
    SentinelFunctionMiddleware sentinelMiddleware,
    IPdpContextAccessor runContext,
    IServiceProvider serviceProvider,
    ILoggerFactory loggerFactory) : IAgentFactory
{
    public async Task<AIAgent> CreateAsync(
        string agentName,
        string? overrideInstructions = null,
        string? overrideModel = null,
        Guid? actingUserId = null,
        ChatHistoryProvider? chatHistory = null,
        bool allowApprovalTools = true,
        CancellationToken ct = default)
    {
        var definition = await ResolveDefinitionAsync(agentName, actingUserId, ct)
            ?? throw new InvalidOperationException($"Agent '{agentName}' could not be resolved.");

        var (settings, contextWindow) = await ResolveSettingsAsync(actingUserId, ct);

        var tools = SelectTools(definition, allowApprovalTools, ct);

        var chatOptions = new ChatOptions
        {
            ModelId = ResolveModel(overrideModel, settings.Ollama),
            Instructions = AgentInstructions.Compose(settings.Harness, overrideInstructions ?? definition.Instructions, [.. tools.OfType<AIFunction>()]),
            Tools = tools,
            Temperature = settings.Ollama?.AgentTemperature ?? ollamaOptions.AgentTemperature,
        };

        chatOptions.AddOllamaOption(OllamaOption.NumCtx, contextWindow);

        var options = new ChatClientAgentOptions
        {
            Name = definition.Name,
            Description = definition.Description,
            ChatOptions = chatOptions,
            ChatHistoryProvider = chatHistory,
            AIContextProviders = [skillsProviderFactory.Create(), learningsProvider],
        };

        var chatClient = AgentChatPipeline.Build(baseChatClient, contextWindow, loggerFactory, serviceProvider);

        // Every Sentium tool call passes the Sentinel policy check (function-calling middleware, after approval).
        return new ChatClientAgent(chatClient, options, loggerFactory, serviceProvider)
            .AsBuilder()
            .Use(sentinelMiddleware.InvokeAsync)
            .Build(serviceProvider);
    }

    public async Task<AIAgent> CreateControlAgentAsync(
        string agentName,
        string? overrideInstructions = null,
        string? overrideModel = null,
        Guid? actingUserId = null,
        CancellationToken ct = default)
    {
        var definition = serviceProvider.GetKeyedService<IAgent>(agentName)
            ?? throw new InvalidOperationException($"Control agent '{agentName}' is not a registered native agent.");

        var (settings, contextWindow) = await ResolveSettingsAsync(actingUserId, ct);

        // Lean by design: no harness, tools, skills, or learnings - just the role and a deterministic temperature.
        var chatOptions = new ChatOptions
        {
            ModelId = ResolveModel(overrideModel, settings.Ollama),
            Instructions = overrideInstructions ?? definition.Instructions,
            Temperature = 0f,
        };

        chatOptions.AddOllamaOption(OllamaOption.NumCtx, contextWindow);

        var chatClient = AgentChatPipeline.Build(baseChatClient, contextWindow, loggerFactory, serviceProvider);

        return new ChatClientAgent(
            chatClient,
            new ChatClientAgentOptions { Name = definition.Name, Description = definition.Description, ChatOptions = chatOptions },
            loggerFactory,
            serviceProvider);
    }

    private async Task<(SettingsSnapshot Settings, int ContextWindow)> ResolveSettingsAsync(Guid? actingUserId, CancellationToken ct)
    {
        // HTTP callers pass no actingUserId; the entry point has already put the user on the run context.
        var settings = await registrySettingsService.GetAsync(actingUserId ?? runContext.UserId, ct);
        var contextWindow = settings.Ollama?.AgentContextWindow is > 0 and var configured ? configured : ollamaOptions.AgentContextWindow;

        return (settings, contextWindow);
    }

    /// <summary>
    /// The agent's curated tool set (all tools when it declares none). Approval-gated tools are dropped for unattended
    /// runs: no human can answer the approval request there, so offering them only stalls the step.
    /// </summary>
    private List<AITool> SelectTools(IAgent definition, bool allowApprovalTools, CancellationToken ct)
    {
        var available = agentToolProvider.GetToolsForAgent(definition.Name, ct);
        var allowed = definition.Tools is { } names ? new HashSet<string>(names, StringComparer.OrdinalIgnoreCase) : null;

        return
        [
            .. available.Where(tool =>
                (allowed is null || allowed.Contains(tool.Name))
                && (allowApprovalTools || tool.GetService<ApprovalRequiredAIFunction>() is null))
        ];
    }

    private async Task<IAgent?> ResolveDefinitionAsync(string agentName, Guid? actingUserId, CancellationToken ct)
    {
        var keyedAgent = serviceProvider.GetKeyedService<IAgent>(agentName);
        if (keyedAgent is not null)
        {
            return keyedAgent;
        }

        var dbAgent = actingUserId is { } uid
            ? await agentRepository.GetAgentByNameForUserAsync(agentName, uid, ct)
            : await agentRepository.GetAgentByNameAsync(agentName, ct);

        return dbAgent is null ? null : new DynamicCustomAgent(dbAgent.Name, dbAgent.Description);
    }

    private string ResolveModel(string? overrideModel, OllamaSettingsSnapshot? ollamaSettings)
    {
        if (!string.IsNullOrWhiteSpace(overrideModel))
        {
            return overrideModel;
        }

        return !string.IsNullOrWhiteSpace(ollamaSettings?.DefaultModel) ? ollamaSettings.DefaultModel : ollamaOptions.DefaultModel;
    }
}
