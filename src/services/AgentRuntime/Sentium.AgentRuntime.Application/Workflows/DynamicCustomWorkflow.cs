using System.Text;
using System.Text.Json;
using Sentium.AgentRuntime.Application.Common.Helpers;
using Sentium.AgentRuntime.Application.Extensions;
using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.WorkflowManagement;
using Sentium.AgentRuntime.Core.Workflows;
using Sentium.Infrastructure.Messaging;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

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

        var activity = root.TryGetProperty("activity", out var activityProp)
            ? activityProp.GetString() ?? trigger.Payload
            : trigger.Payload;

        if (!root.TryGetProperty("workflowId", out var workflowIdProp) || !workflowIdProp.TryGetGuid(out var workflowId))
        {
            return new WorkflowResult { Explanation = "Custom workflow trigger is missing a valid workflowId." };
        }

        if (root.TryGetProperty("workspaceId", out var wsProp)
            && wsProp.ValueKind == JsonValueKind.String
            && Guid.TryParse(wsProp.GetString(), out _))
        {
            activity = $"{activity}\n\n[Workspace context: ID = {wsProp.GetString()}. Use list_workspaces and list_workspace_files tools to discover and read files in this workspace before answering.]";
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
            var agentModel = !string.IsNullOrWhiteSpace(agentDetails.Model) ? agentDetails.Model : null;
            var agent = await factory.CreateAsync(agentDetails.Name, overrideInstructions: agentDetails.Description, overrideModel: agentModel, actingUserId: trigger.UserId, ct: ct);
            squadAgents.Add(agent);
        }

        var squadWorkflow = AgentWorkflowBuilder.BuildSequential($"custom-{workflowDef.Name}", squadAgents);
        var messages = new List<ChatMessage> { new(ChatRole.User, activity) };

        await using var run = await InProcessExecution.RunStreamingAsync(squadWorkflow, messages, cancellationToken: ct);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

        var streamLog = new StreamLogAccumulator();
        List<ChatMessage> finalHistory = [];

        await foreach (var evt in run.WatchStreamAsync(ct).ConfigureAwait(false).WithCancellation(ct))
        {
            if (evt is AgentResponseUpdateEvent e)
            {
                var author = e.Update.AuthorName ?? workflowDef.Name;

                if (e.Update.Contents.OfType<TextReasoningContent>().FirstOrDefault() is { } reasoning && !string.IsNullOrEmpty(reasoning.Text))
                {
                    await nats.StreamAgentUpdateAsync(trigger.TriggerType, author, reasoning.Text, AgentUpdateTypes.Thought, ct);
                    streamLog.Add(author, reasoning.Text, AgentUpdateTypes.Thought);
                }

                foreach (var call in e.Update.Contents.OfType<FunctionCallContent>())
                {
                    var toolLabel = $"Calling {call.Name}...";
                    await nats.StreamAgentUpdateAsync(trigger.TriggerType, author, toolLabel, AgentUpdateTypes.Tool, ct);
                    streamLog.Add(author, toolLabel, AgentUpdateTypes.Tool);
                }

                if (!string.IsNullOrEmpty(e.Update.Text))
                {
                    await nats.StreamAgentUpdateAsync(trigger.TriggerType, author, e.Update.Text, ct);
                    streamLog.Add(author, e.Update.Text, AgentUpdateTypes.Message);
                }
            }
            else if (evt is WorkflowOutputEvent outputEvt)
            {
                finalHistory = outputEvt.As<List<ChatMessage>>()!;
            }
        }

        string explanation;
        if (finalHistory.Count > 0)
        {
            var explanationSb = new StringBuilder();
            foreach (var m in finalHistory)
            {
                if (explanationSb.Length > 0)
                {
                    explanationSb.Append('\n');
                }

                explanationSb.Append(m.Role).Append(": ").Append(m.Text);
            }
            explanation = explanationSb.ToString();
        }
        else
        {
            explanation = $"Custom workflow '{workflowDef.Name}' completed with no output.";
        }

        return new WorkflowResult
        {
            Explanation = explanation,
            StreamLog = streamLog.Entries,
            WorkflowId = workflowId,
            UserId = workflowDef.UserId
        };
    }
}
