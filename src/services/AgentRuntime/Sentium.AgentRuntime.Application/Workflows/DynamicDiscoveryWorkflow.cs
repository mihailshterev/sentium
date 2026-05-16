using System.Text;
using System.Text.Json;
using Sentium.AgentRuntime.Application.Common.Helpers;
using Sentium.AgentRuntime.Application.Extensions;
using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Workflows;
using Sentium.Infrastructure.Messaging;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Sentium.AgentRuntime.Application.Workflows;

public sealed class DynamicDiscoveryWorkflow(
    IAgentFactory factory,
    IAgentRegistry registry,
    IAgentManager agentManager,
    IEventBus nats) : IAgentWorkflow
{
    public WorkflowType Type => WorkflowType.Dynamic;

    public async Task<WorkflowResult> ExecuteAsync(WorkflowTrigger trigger, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        string? workspaceContext = null;
        try
        {
            using var payloadDoc = JsonDocument.Parse(trigger.Payload);
            var root = payloadDoc.RootElement;
            if (root.TryGetProperty("workspaceId", out var wsProp)
                && wsProp.ValueKind == JsonValueKind.String
                && Guid.TryParse(wsProp.GetString(), out _))
            {
                workspaceContext = wsProp.GetString();
            }
        }
        catch { }

        var dbAgents = await agentManager.GetAgentsAsync(ct);
        var dbAgentMap = dbAgents.ToDictionary(a => a.Name, a => a.Description, StringComparer.OrdinalIgnoreCase);
        var dbAgentModelMap = dbAgents.ToDictionary(a => a.Name, a => a.Model, StringComparer.OrdinalIgnoreCase);

        var plannerInstructions = BuildPlannerInstructions(dbAgents);
        var planner = await factory.CreateAsync(AgentRole.Planner, overrideInstructions: plannerInstructions, ct: ct);
        var plannerSession = await planner.CreateSessionAsync(ct);

        var streamLog = new StreamLogAccumulator();
        var rawPlanBuilder = new StringBuilder();
        var plannerInput = workspaceContext is not null
            ? $"{trigger.Payload}\n\n[Workspace context: ID = {workspaceContext}. Use list_workspaces and list_workspace_files tools to explore available files.]"
            : trigger.Payload;

        await foreach (var update in planner.RunStreamingAsync(plannerInput, plannerSession, cancellationToken: ct))
        {
            if (update.Contents.OfType<TextReasoningContent>().FirstOrDefault() is { } reasoning && !string.IsNullOrEmpty(reasoning.Text))
            {
                await nats.StreamAgentUpdateAsync(trigger.TriggerType, AgentRole.Planner, reasoning.Text, AgentUpdateTypes.Thought, ct);
                streamLog.Add(AgentRole.Planner, reasoning.Text, AgentUpdateTypes.Thought);
            }

            foreach (var call in update.Contents.OfType<FunctionCallContent>())
            {
                var toolLabel = $"Calling {call.Name}...";
                await nats.StreamAgentUpdateAsync(trigger.TriggerType, AgentRole.Planner, toolLabel, AgentUpdateTypes.Tool, ct);
                streamLog.Add(AgentRole.Planner, toolLabel, AgentUpdateTypes.Tool);
            }

            if (!string.IsNullOrEmpty(update.Text))
            {
                rawPlanBuilder.Append(update.Text);
                await nats.StreamAgentUpdateAsync(trigger.TriggerType, AgentRole.Planner, update.Text, ct);
                streamLog.Add(AgentRole.Planner, update.Text, AgentUpdateTypes.Message);
            }
        }

        var rolesToExecute = LlmParser.ParseAgentRoles(rawPlanBuilder.ToString(), dbAgentMap, registry);
        if (rolesToExecute.Count == 0)
        {
            return new WorkflowResult { Explanation = "Planner failed to identify required agents.", StreamLog = streamLog.Entries };
        }

        var squadAgents = new List<AIAgent>();
        foreach (var role in rolesToExecute)
        {
            var overrideInstructions = dbAgentMap.TryGetValue(role, out var desc) ? desc : null;
            var overrideModel = dbAgentModelMap.TryGetValue(role, out var mdl) && !string.IsNullOrWhiteSpace(mdl) ? mdl : null;
            var agent = await factory.CreateAsync(role, overrideInstructions: overrideInstructions, overrideModel: overrideModel, ct: ct);
            squadAgents.Add(agent);
        }

        var squadWorkflow = AgentWorkflowBuilder.BuildSequential("dynamic-squad", squadAgents);
        var squadInput = workspaceContext is not null
            ? $"{trigger.Payload}\n\n[Workspace context: ID = {workspaceContext}. Use list_workspaces and list_workspace_files tools to explore available files.]"
            : trigger.Payload;
        var messages = new List<ChatMessage> { new(ChatRole.User, squadInput) };

        await using var run = await InProcessExecution.RunStreamingAsync(squadWorkflow, messages, cancellationToken: ct);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

        List<ChatMessage> finalHistory = [];
        await foreach (var evt in run.WatchStreamAsync(ct).ConfigureAwait(false).WithCancellation(ct))
        {
            if (evt is AgentResponseUpdateEvent e)
            {
                var author = e.Update.AuthorName ?? "Squad";

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

        var validator = await factory.CreateAsync(AgentRole.Validator, ct: ct);
        var validatorSession = await validator.CreateSessionAsync(ct);

        var squadFindingsSb = new StringBuilder();
        foreach (var m in finalHistory)
        {
            if (squadFindingsSb.Length > 0)
            {
                squadFindingsSb.Append('\n');
            }

            squadFindingsSb.Append(m.Role).Append(": ").Append(m.Text);
        }
        var validationInput = $"Original Request: {trigger.Payload}\n\nSquad Findings:\n{squadFindingsSb}";
        var finalFullResponse = new StringBuilder();

        await foreach (var update in validator.RunStreamingAsync(validationInput, validatorSession, cancellationToken: ct))
        {
            if (update.Contents.OfType<TextReasoningContent>().FirstOrDefault() is { } reasoning && !string.IsNullOrEmpty(reasoning.Text))
            {
                await nats.StreamAgentUpdateAsync(trigger.TriggerType, AgentRole.Validator, reasoning.Text, AgentUpdateTypes.Thought, ct);
                streamLog.Add(AgentRole.Validator, reasoning.Text, AgentUpdateTypes.Thought);
            }

            foreach (var call in update.Contents.OfType<FunctionCallContent>())
            {
                var toolLabel = $"Calling {call.Name}...";
                await nats.StreamAgentUpdateAsync(trigger.TriggerType, AgentRole.Validator, toolLabel, AgentUpdateTypes.Tool, ct);
                streamLog.Add(AgentRole.Validator, toolLabel, AgentUpdateTypes.Tool);
            }

            if (!string.IsNullOrEmpty(update.Text))
            {
                finalFullResponse.Append(update.Text);
                await nats.StreamAgentUpdateAsync(trigger.TriggerType, AgentRole.Validator, update.Text, ct);
                streamLog.Add(AgentRole.Validator, update.Text, AgentUpdateTypes.Message);
            }
        }

        return LlmParser.ParseWorkflowResult(finalFullResponse.ToString(), rolesToExecute, streamLog.Entries);
    }

    private string BuildPlannerInstructions(IReadOnlyList<Core.Dtos.AgentResponse> dbAgents)
    {
        var builtInAgents = registry.GetRegisteredNames()
            .Where(n => !string.Equals(n, AgentRole.Planner, StringComparison.OrdinalIgnoreCase)
                     && !string.Equals(n, AgentRole.Validator, StringComparison.OrdinalIgnoreCase));

        var agentLines = new StringBuilder();

        foreach (var name in builtInAgents)
        {
            agentLines.AppendLine($"- {name}: {registry.GetInstructions(name)}");
        }

        foreach (var agent in dbAgents)
        {
            agentLines.AppendLine($"- {agent.Name}: {agent.Description}");
        }

        return PlannerTemplate.Build(agentLines.ToString());
    }
}
