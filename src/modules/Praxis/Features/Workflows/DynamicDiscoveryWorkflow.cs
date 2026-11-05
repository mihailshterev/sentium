using Sentium.Praxis.Features.Orchestration;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Workflows.Squad;

namespace Sentium.Praxis.Features.Workflows;

/// <summary>
/// Dynamic squad: the planner picks the specialists (native ones plus the user's own agents) and their tasks.
/// </summary>
public sealed class DynamicDiscoveryWorkflow(
    SquadWorkflowRunner squad,
    IEnumerable<IAgent> nativeAgents,
    IAgentRepository agentRepository) : IAgentWorkflow
{
    /// <summary>Native roles that steer the squad rather than doing specialist work, so they are never planned as steps.</summary>
    private static readonly HashSet<string> ControlRoles = new([AgentRole.Orchestrator, AgentRole.Validator, AgentRole.Summarizer], StringComparer.OrdinalIgnoreCase);

    public WorkflowType Type => WorkflowType.Dynamic;

    public async Task<WorkflowResult> ExecuteAsync(WorkflowTrigger trigger, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        var request = WorkspaceContextPrompt.Augment(trigger.ResolveActivity(), WorkspaceContextPrompt.TryExtract(trigger.Payload));

        var roster = new List<SquadMember>();
        roster.AddRange(nativeAgents
            .Where(a => !ControlRoles.Contains(a.Name))
            .DistinctBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .Select(a => new SquadMember(a.Name, a.Description, a.Instructions)));

        var userAgents = await agentRepository.GetAgentsForUserAsync(trigger.UserId, ct);
        roster.AddRange(userAgents.Select(a => new SquadMember(a.Name, a.Description, a.Description, string.IsNullOrWhiteSpace(a.Model) ? null : a.Model)));

        var log = new StreamLogAccumulator();
        var result = await squad.RunAsync(trigger, request, roster, fixedPlan: null, log, ct);

        return SquadResultMapper.ToWorkflowResult(result, log, "No specialist agents were needed for this request.", workflowId: null, trigger.UserId);
    }
}
