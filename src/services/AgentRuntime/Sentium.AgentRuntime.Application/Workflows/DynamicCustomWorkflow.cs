using System.Text.Json;
using Sentium.AgentRuntime.Application.Common.Helpers;
using Sentium.AgentRuntime.Application.Extensions;
using Sentium.AgentRuntime.Core.Agents;
using AgentResponse = Sentium.AgentRuntime.Core.Dtos.AgentResponse;
using Sentium.AgentRuntime.Core.WorkflowManagement;
using Sentium.AgentRuntime.Core.Workflows;
using Sentium.Infrastructure.Messaging;
using Microsoft.Agents.AI;

namespace Sentium.AgentRuntime.Application.Workflows;

public sealed class DynamicCustomWorkflow(
    IAgentFactory factory,
    IAgentRepository agentRepository,
    IWorkflowService workflowService,
    IEventBus nats) : IAgentWorkflow
{
    public WorkflowType Type => WorkflowType.Custom;

    public async Task<WorkflowResult> ExecuteAsync(WorkflowTrigger trigger, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        using var doc = JsonDocument.Parse(trigger.Payload);
        var root = doc.RootElement;

        var activity = root.TryGetProperty("activity", out var activityProp)
            ? activityProp.GetString() ?? trigger.Payload
            : trigger.Payload;

        if (!root.TryGetProperty("workflowId", out var workflowIdProp) || !workflowIdProp.TryGetGuid(out var workflowId))
        {
            return new WorkflowResult { Explanation = "Custom workflow trigger is missing a valid workflowId." };
        }

        if (root.TryGetProperty("workspaceId", out var wsProp) && wsProp.ValueKind == JsonValueKind.String && Guid.TryParse(wsProp.GetString(), out _))
        {
            activity = $"{activity}\n\n[Workspace context: ID = {wsProp.GetString()}. Use list_workspaces and list_workspace_files tools to discover and read files in this workspace before answering.]";
        }

        var workflowDef = await workflowService.GetWorkflowAsync(workflowId, ct);
        if (workflowDef is null)
        {
            return new WorkflowResult { Explanation = $"Workflow '{workflowId}' was not found." };
        }

        var orderedRefs = workflowDef.Agents.OrderBy(a => a.Order).ToList();

        if (orderedRefs.Count == 0)
        {
            return new WorkflowResult { Explanation = $"Workflow '{workflowDef.Name}' has no agents configured." };
        }

        var resolved = new List<AgentResponse>();
        foreach (var agentRef in orderedRefs)
        {
            var agentDetails = await agentRepository.GetAgentByIdAsync(agentRef.AgentId, ct);
            if (agentDetails is not null)
            {
                resolved.Add(agentDetails);
            }
        }

        var rosterNames = resolved.Select(a => a.Name).ToList();

        var squadAgents = new List<AIAgent>();
        var roster = new List<SquadMember>();

        for (var i = 0; i < resolved.Count; i++)
        {
            var agentDetails = resolved[i];
            var agentModel = !string.IsNullOrWhiteSpace(agentDetails.Model) ? agentDetails.Model : null;

            var directive = SquadCollaborationPrompt.Build(agentDetails.Name, assignment: null, i + 1, resolved.Count, rosterNames);
            var instructions = $"{agentDetails.Description}\n\n{directive}";

            var agent = await factory.CreateAsync(agentDetails.Name, overrideInstructions: instructions, overrideModel: agentModel, actingUserId: trigger.UserId, ct: ct);
            squadAgents.Add(agent);

            roster.Add(new SquadMember(agentDetails.Name, agentDetails.Description));
        }

        var streamLog = new StreamLogAccumulator();

        var duplicateNames = roster
            .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateNames.Count > 0)
        {
            var warning = $"Warning: workflow '{workflowDef.Name}' has duplicate agent names ({string.Join(", ", duplicateNames)}). Targeted re-runs and per-agent attribution may be unreliable - give each agent a unique name.";
            await nats.StreamAgentUpdateAsync(trigger.TriggerType, "System", warning, AgentUpdateTypes.Status, ct);
            streamLog.Add("System", warning, AgentUpdateTypes.Status);
        }

        var loop = new AgenticRefinementLoop(factory, nats);
        var outcome = await loop.RunAsync(trigger, $"custom-{workflowDef.Name}", squadAgents, activity, streamLog, ct, roster);

        var explanation = string.IsNullOrWhiteSpace(outcome.SquadText) ? $"Custom workflow '{workflowDef.Name}' completed with no output." : outcome.SquadText;

        return new WorkflowResult
        {
            Explanation = explanation,
            StreamLog = streamLog.Entries,
            WorkflowId = workflowId,
            UserId = workflowDef.UserId
        };
    }
}
