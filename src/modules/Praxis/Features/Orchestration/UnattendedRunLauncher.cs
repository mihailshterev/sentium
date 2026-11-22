using Microsoft.Extensions.AI;
using Sentium.Infrastructure.Security;
using Sentium.Praxis.Contracts;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Agents.Runtime;
using Sentium.Praxis.Features.WorkflowManagement;
using Sentium.Praxis.Features.Workflows;

namespace Sentium.Praxis.Features.Orchestration;

/// <summary>
/// A Schedule's run is a custom workflow run: a saved workflow, or a one-step plan for one agent (see
/// <see cref="DynamicCustomWorkflow"/>). Lookups act as the owner, whoever is calling, so a Sovereign editing another
/// user's Schedule sees that user's agents and workflows.
/// </summary>
internal sealed class UnattendedRunLauncher(
    IWorkflowRunQueue queue,
    ScheduledRunGate gate,
    IStreamRelay streamRelay,
    IServiceScopeFactory scopeFactory,
    ILogger<UnattendedRunLauncher> logger) : IUnattendedRunLauncher
{
    public async Task<UnattendedRunLaunch> LaunchAsync(UnattendedRunRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!gate.TryAcquire(request.ScheduleId))
        {
            return UnattendedRunLaunch.SkippedStillRunning;
        }

        var preApproved = new HashSet<string>(request.PreApprovedTools, StringComparer.OrdinalIgnoreCase);
        var trigger = new WorkflowTrigger
        {
            TriggerType = WorkflowTriggerTypes.CustomWorkflow,
            Activity = request.Prompt,
            WorkflowId = request.Kind == UnattendedRunKind.Workflow ? request.WorkflowId : null,
            AgentName = request.Kind == UnattendedRunKind.Agent ? request.AgentName : null,
            UserId = request.OwnerId,
            StreamId = Guid.NewGuid().ToString("N"),
            Schedule = new ScheduledRun(request.ScheduleId, request.ScheduleName, new UnattendedRunPolicy(preApproved)),
        };

        // Claimed like an interactive run, so only the owner (or a Sovereign) can follow it.
        streamRelay.Claim(trigger.StreamId, request.OwnerId);

        try
        {
            await queue.EnqueueAsync(trigger, ct);
        }
        catch
        {
            gate.Release(request.ScheduleId);
            throw;
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Queued the run of schedule {ScheduleId} for user {UserId} (stream {StreamId}).", request.ScheduleId, request.OwnerId, trigger.StreamId);
        }

        return UnattendedRunLaunch.Queued;
    }

    public async Task<UnattendedRunTarget?> ResolveTargetAsync(UnattendedRunKind kind, string? agentName, Guid? workflowId, Guid ownerId, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var services = scope.ServiceProvider;
        services.GetRequiredService<SystemScopeContext>().ActAs(ownerId);

        var agentFactory = services.GetRequiredService<IAgentFactory>();

        return kind switch
        {
            UnattendedRunKind.Agent => await ResolveAgentAsync(services, agentFactory, agentName, ownerId, ct),
            UnattendedRunKind.Workflow => await ResolveWorkflowAsync(services, agentFactory, workflowId, ownerId, ct),
            _ => null,
        };
    }

    private static async Task<UnattendedRunTarget?> ResolveAgentAsync(IServiceProvider services, IAgentFactory agentFactory, string? agentName, Guid ownerId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(agentName))
        {
            return null;
        }

        var entry = await services.GetRequiredService<IAgentRoster>().FindAsync(agentName, ownerId, ct);
        if (entry is null)
        {
            return null;
        }

        var gated = await GatedToolNamesAsync(agentFactory, entry.Name, ownerId, ct);
        return new UnattendedRunTarget(entry.Name, Describe(services, gated));
    }

    private static async Task<UnattendedRunTarget?> ResolveWorkflowAsync(IServiceProvider services, IAgentFactory agentFactory, Guid? workflowId, Guid ownerId, CancellationToken ct)
    {
        if (workflowId is not { } id)
        {
            return null;
        }

        var workflow = await services.GetRequiredService<IWorkflowService>().GetWorkflowAsync(id, ct);
        if (workflow is null)
        {
            return null;
        }

        var agentRepository = services.GetRequiredService<IAgentRepository>();
        var gated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var agentRef in workflow.Agents)
        {
            if (await agentRepository.GetAgentByIdAsync(agentRef.AgentId, ct) is { } agent)
            {
                gated.UnionWith(await GatedToolNamesAsync(agentFactory, agent.Name, ownerId, ct));
            }
        }

        return new UnattendedRunTarget(workflow.Name, Describe(services, gated));
    }

    private static async Task<IReadOnlyCollection<string>> GatedToolNamesAsync(IAgentFactory agentFactory, string agentName, Guid ownerId, CancellationToken ct)
    {
        var tools = await agentFactory.ResolveToolsAsync(agentName, ownerId, ct);
        if (tools is null)
        {
            return [];
        }

        return [.. tools.Where(tool => tool.GetService<ApprovalRequiredAIFunction>() is not null).Select(tool => tool.Name)];
    }

    private static List<ToolDescriptor> Describe(IServiceProvider services, IEnumerable<string> toolNames)
    {
        var catalog = services.GetRequiredService<IToolCatalog>().GetTools().ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

        return
        [
            .. toolNames
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(name => catalog.TryGetValue(name, out var descriptor)
                    ? descriptor
                    : new ToolDescriptor(name, string.Empty, string.Empty, string.Empty, string.Empty, RequiresApproval: true))
                .OrderBy(d => d.Name, StringComparer.Ordinal)
        ];
    }
}
