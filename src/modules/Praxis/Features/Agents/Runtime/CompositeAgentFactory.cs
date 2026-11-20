using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Sentium.Infrastructure.Inference;
using Sentium.Praxis.Contracts;
using Sentium.Praxis.Features.Learnings;
using Sentium.Praxis.Features.Models;
using Sentium.Praxis.Features.Registry;
using Sentium.Praxis.Features.Sentinel;
using Sentium.Praxis.Features.Skills;
using Sentium.Praxis.Features.Tools;
using Sentium.Praxis.Features.Workspaces;

namespace Sentium.Praxis.Features.Agents.Runtime;

/// <summary>
/// Builds a <see cref="ChatClientAgent"/> per request on the shared agent base client, adding per-agent function
/// invocation and context-window compaction (see <see cref="AgentChatPipeline"/>). Everything else that varies per agent
/// is expressed through native Agent Framework / Microsoft.Extensions.AI options: the model via
/// <see cref="ChatOptions.ModelId"/>, the composed instructions via <see cref="ChatOptions.Instructions"/> (one system
/// message), skills / learnings / workspace files via <see cref="ChatClientAgentOptions.AIContextProviders"/>, and the Sentinel tool guard
/// as function-calling middleware.
/// </summary>
internal sealed class CompositeAgentFactory(
    [FromKeyedServices(PraxisModuleExtensions.AgentChatClientKey)] IChatClient baseChatClient,
    IOptions<InferenceOptions> inferenceOptions,
    IModelContextWindowProvider contextWindows,
    IAgentToolProvider agentToolProvider,
    IEnumerable<IAgentToolSource> toolSources,
    IAgentRepository agentRepository,
    IBuiltInAgentService builtInAgents,
    IRegistrySettingsService registrySettingsService,
    AgentSkillsProviderFactory skillsProviderFactory,
    LearningsContextProvider learningsProvider,
    WorkspaceContextProviderFactory workspaceContextFactory,
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
        var blueprint = await ResolveBlueprintAsync(agentName, overrideModel, actingUserId, allowApprovalTools, ct)
            ?? throw new InvalidOperationException($"Agent '{agentName}' could not be resolved.");

        var chatOptions = new ChatOptions
        {
            ModelId = blueprint.Model,
            Instructions = blueprint.ComposeInstructions(overrideInstructions),
            Tools = blueprint.Tools,
            Temperature = blueprint.Temperature,
        };

        var options = new ChatClientAgentOptions
        {
            Name = blueprint.Definition.Name,
            Description = blueprint.Definition.Description,
            ChatOptions = chatOptions,
            ChatHistoryProvider = chatHistory,
            AIContextProviders = [skillsProviderFactory.Create(), learningsProvider, workspaceContextFactory.Create(blueprint.ContextWindow)],
        };

        var chatClient = AgentChatPipeline.Build(baseChatClient, blueprint.ContextWindow, loggerFactory, serviceProvider);

        // Every Sentium tool call passes the Sentinel policy check (function-calling middleware, after approval).
        return new ChatClientAgent(chatClient, options, loggerFactory, serviceProvider)
            .AsBuilder()
            .Use(sentinelMiddleware.InvokeAsync)
            .Build(serviceProvider);
    }

    public async Task<AgentBlueprint?> ResolveBlueprintAsync(
        string agentName,
        string? overrideModel,
        Guid? actingUserId,
        bool allowApprovalTools,
        CancellationToken ct = default)
    {
        var definition = await ResolveDefinitionAsync(agentName, actingUserId, ct);
        if (definition is null)
        {
            return null;
        }

        var (settings, model, contextWindow) = await ResolveSettingsAsync(overrideModel, actingUserId, ct);
        var tools = await SelectToolsAsync(definition, allowApprovalTools, ct);

        return new AgentBlueprint(definition, settings, model, contextWindow, tools);
    }

    public async Task<IReadOnlyList<AITool>?> ResolveToolsAsync(string agentName, Guid? actingUserId, CancellationToken ct = default)
    {
        var definition = await ResolveDefinitionAsync(agentName, actingUserId, ct);
        return definition is null ? null : await SelectToolsAsync(definition, allowApprovalTools: true, ct);
    }

    public async Task<string> ResolveModelAsync(string? overrideModel, Guid? actingUserId, CancellationToken ct = default)
    {
        var settings = await registrySettingsService.GetAsync(actingUserId ?? runContext.UserId, ct);
        return ResolveModel(overrideModel, settings.Inference);
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
    /// The agent's curated tool set (all built-in tools when it declares none). External-source (e.g. MCP) tools are opt-in: an agent gets one
    /// only when its tool list names it explicitly, so registering a server never silently widens what existing agents
    /// can do. Without <paramref name="allowApprovalTools"/> no human can answer an approval request, so a gated tool is
    /// either dropped (a workflow started from the portal) or, in a Schedule's run, handled by the run's
    /// <see cref="UnattendedRunPolicy"/> (see <see cref="UnattendedToolGate"/>).
    /// </summary>
    private async Task<List<AITool>> SelectToolsAsync(IAgent definition, bool allowApprovalTools, CancellationToken ct)
    {
        var available = agentToolProvider.GetToolsForAgent(definition.Name, ct);
        var allowed = definition.Tools is { } names ? new HashSet<string>(names, StringComparer.OrdinalIgnoreCase) : null;

        var candidates = available.Where(tool => allowed is null || allowed.Contains(tool.Name));

        if (allowed is not null)
        {
            foreach (var source in toolSources)
            {
                candidates = candidates.Concat(await source.GetFunctionsAsync(allowed, ct));
            }
        }

        if (allowApprovalTools)
        {
            return [.. candidates];
        }

        if (runContext.Unattended is { } policy)
        {
            return [.. candidates.Select(tool => UnattendedToolGate.Apply(tool, policy))];
        }

        return [.. candidates.Where(tool => tool.GetService<ApprovalRequiredAIFunction>() is null)];
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
