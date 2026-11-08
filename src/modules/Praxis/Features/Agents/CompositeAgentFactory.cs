using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Sentium.Infrastructure.Inference;
using Sentium.Praxis.Features.Learnings;
using Sentium.Praxis.Features.Mcp;
using Sentium.Praxis.Features.Models;
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
    IOptions<InferenceOptions> inferenceOptions,
    IModelContextWindowProvider contextWindows,
    IAgentToolProvider agentToolProvider,
    IMcpToolCatalog mcpTools,
    IMcpSettingsProvider mcpSettings,
    IAgentRepository agentRepository,
    IBuiltInAgentService builtInAgents,
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

        var (settings, model, contextWindow) = await ResolveSettingsAsync(overrideModel, actingUserId, ct);

        var tools = await SelectToolsAsync(definition, allowApprovalTools, ct);

        var chatOptions = new ChatOptions
        {
            ModelId = model,
            Instructions = AgentInstructions.Compose(settings.Harness, overrideInstructions ?? definition.Instructions, [.. tools.OfType<AIFunction>()]),
            Tools = tools,
            Temperature = settings.Inference?.AgentTemperature ?? InferenceSettingsSnapshot.Default.AgentTemperature,
        };

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

        var (settings, model, contextWindow) = await ResolveSettingsAsync(overrideModel, actingUserId, ct);

        // Lean by design: no harness, tools, skills, or learnings - just the role and a deterministic temperature.
        var chatOptions = new ChatOptions
        {
            ModelId = model,
            Instructions = overrideInstructions ?? definition.Instructions,
            Temperature = 0f,
        };

        var chatClient = AgentChatPipeline.Build(baseChatClient, contextWindow, loggerFactory, serviceProvider);

        return new ChatClientAgent(
            chatClient,
            new ChatClientAgentOptions { Name = definition.Name, Description = definition.Description, ChatOptions = chatOptions },
            loggerFactory,
            serviceProvider);
    }

    /// <summary>
    /// The acting user's settings, the model to run (override → Registry default → configured default), and that
    /// model's per-request context window, which sizes compaction. The window is fixed per model by the inference
    /// server, so it is never sent with the request.
    /// </summary>
    private async Task<(SettingsSnapshot Settings, string Model, int ContextWindow)> ResolveSettingsAsync(
        string? overrideModel,
        Guid? actingUserId,
        CancellationToken ct)
    {
        // HTTP callers pass no actingUserId; the entry point has already put the user on the run context.
        var settings = await registrySettingsService.GetAsync(actingUserId ?? runContext.UserId, ct);
        var model = ResolveModel(overrideModel, settings.Inference);
        var contextWindow = await contextWindows.GetContextWindowAsync(model, ct);

        return (settings, model, contextWindow);
    }

    /// <summary>
    /// The agent's curated tool set (all built-in tools when it declares none). MCP tools are opt-in: an agent gets one
    /// only when its tool list names it explicitly, so registering a server never silently widens what existing agents
    /// can do. Approval-gated tools are dropped for unattended runs: no human can answer the approval request there, so
    /// offering them only stalls the step.
    /// </summary>
    private async Task<List<AITool>> SelectToolsAsync(IAgent definition, bool allowApprovalTools, CancellationToken ct)
    {
        var available = agentToolProvider.GetToolsForAgent(definition.Name, ct);
        var allowed = definition.Tools is { } names ? new HashSet<string>(names, StringComparer.OrdinalIgnoreCase) : null;

        var candidates = available.Where(tool => allowed is null || allowed.Contains(tool.Name));

        if (allowed is not null && allowed.Any(McpNames.IsMcpToolName) && (await mcpSettings.GetAsync(ct)).Enabled)
        {
            await mcpTools.EnsureLoadedAsync(ct);
            candidates = candidates.Concat(mcpTools.GetFunctions(allowed));
        }

        return [.. candidates.Where(tool => allowApprovalTools || tool.GetService<ApprovalRequiredAIFunction>() is null)];
    }

    private async Task<IAgent?> ResolveDefinitionAsync(string agentName, Guid? actingUserId, CancellationToken ct)
    {
        var keyedAgent = serviceProvider.GetKeyedService<IAgent>(agentName);
        if (keyedAgent is not null)
        {
            return await builtInAgents.ApplyOverrideAsync(keyedAgent, actingUserId, ct);
        }

        var dbAgent = actingUserId is { } uid
            ? await agentRepository.GetAgentByNameForUserAsync(agentName, uid, ct)
            : await agentRepository.GetAgentByNameAsync(agentName, ct);

        return dbAgent is null ? null : new DynamicCustomAgent(dbAgent.Name, dbAgent.Description, dbAgent.Tools);
    }

    private string ResolveModel(string? overrideModel, InferenceSettingsSnapshot? inferenceSettings)
    {
        if (!string.IsNullOrWhiteSpace(overrideModel))
        {
            return overrideModel;
        }

        return !string.IsNullOrWhiteSpace(inferenceSettings?.DefaultModel) ? inferenceSettings.DefaultModel : inferenceOptions.Value.DefaultModel;
    }
}
