using System.Text.Json;
using Sentium.AgentRuntime.Application.Extensions;
using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.WorkflowManagement;
using Sentium.AgentRuntime.Core.Workflows;
using Sentium.Infrastructure.Messaging;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Sentium.AgentRuntime.Application.Workflows;

public sealed class DynamicCustomWorkflow(
    IAgentFactory factory,
    IAgentManager agentManager,
    IWorkflowService workflowService,
    IEventBus nats) : IAgentWorkflow
{
    public WorkflowType Type => WorkflowType.Custom;

    public async Task<WorkflowResult> ExecuteAsync(WorkflowTrigger trigger, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        using var doc = JsonDocument.Parse(trigger.Payload);
        var root = doc.RootElement;

        var activity = root.TryGetProperty("activity", out var activityProp) ? activityProp.GetString() ?? trigger.Payload : trigger.Payload;

        if (!root.TryGetProperty("workflowId", out var workflowIdProp) || !workflowIdProp.TryGetGuid(out var workflowId))
        {
            return new WorkflowResult { Explanation = "Custom workflow trigger is missing a valid workflowId." };
        }

        var workflowDef = await workflowService.GetWorkflowAsync(workflowId, ct);
        var orderedRefs = workflowDef.Agents.OrderBy(a => a.Order).ToList();

        if (orderedRefs.Count == 0)
        {
            return new WorkflowResult { Explanation = $"Workflow '{workflowDef.Name}' has no agents configured." };
        }

        var squadAgents = new List<AIAgent>();
        foreach (var agentRef in orderedRefs)
        {
            var agentDetails = await agentManager.GetAgentByIdAsync(agentRef.AgentId, ct);
            var agent = await factory.CreateAsync(agentDetails.Name, overrideInstructions: agentDetails.Description, ct: ct);
            squadAgents.Add(agent);
        }

        var squadWorkflow = AgentWorkflowBuilder.BuildSequential($"custom-{workflowDef.Name}", squadAgents);

        var messages = new List<ChatMessage> { new(ChatRole.User, activity) };

        await using var run = await InProcessExecution.RunStreamingAsync(squadWorkflow, messages, cancellationToken: ct);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

        List<ChatMessage> finalHistory = [];
        await foreach (var evt in run.WatchStreamAsync(ct).ConfigureAwait(false).WithCancellation(ct))
        {
            if (evt is AgentResponseUpdateEvent e)
            {
                await nats.StreamAgentUpdateAsync(trigger.TriggerType, e.Update.AuthorName ?? workflowDef.Name, e.Update.Text, ct);
            }
            else if (evt is WorkflowOutputEvent outputEvt)
            {
                finalHistory = outputEvt.As<List<ChatMessage>>()!;
            }
        }

        var explanation = finalHistory.Count > 0
            ? string.Join("\n", finalHistory.Select(m => $"{m.Role}: {m.Text}"))
            : $"Custom workflow '{workflowDef.Name}' completed with no output.";

        return new WorkflowResult { Explanation = explanation };
    }
}
