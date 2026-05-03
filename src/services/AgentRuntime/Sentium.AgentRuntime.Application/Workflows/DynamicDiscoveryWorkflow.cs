using Sentium.AgentRuntime.Application.Common.Helpers;
using Sentium.AgentRuntime.Application.Extensions;
using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Workflows;
using Sentium.Infrastructure.Messaging;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using System.Text;

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

        var dbAgents = await agentManager.GetAgentsAsync(ct);
        var dbAgentMap = dbAgents.ToDictionary(a => a.Name, a => a.Description, StringComparer.OrdinalIgnoreCase);

        var plannerInstructions = BuildPlannerInstructions(dbAgents);

        var planner = await factory.CreateAsync(AgentRole.Planner, overrideInstructions: plannerInstructions, ct: ct);
        var plannerSession = await planner.CreateSessionAsync(ct);

        var rawPlan = string.Empty;
        await foreach (var update in planner.RunStreamingAsync(trigger.Payload, plannerSession, cancellationToken: ct))
        {
            rawPlan += update.Text;
            await nats.StreamAgentUpdateAsync(trigger.TriggerType, AgentRole.Planner, update.Text, ct);
        }

        var rolesToExecute = LlmParser.ParseAgentRoles(rawPlan, dbAgentMap, registry);
        if (rolesToExecute.Count == 0)
        {
            return new WorkflowResult { Explanation = "Planner failed to identify required agents." };
        }

        var squadAgents = new List<AIAgent>();
        foreach (var role in rolesToExecute)
        {
            var overrideInstructions = dbAgentMap.TryGetValue(role, out var desc) ? desc : null;
            var agent = await factory.CreateAsync(role, overrideInstructions: overrideInstructions, ct: ct);
            squadAgents.Add(agent);
        }

        var squadWorkflow = AgentWorkflowBuilder.BuildSequential("dynamic-squad", squadAgents);

        var messages = new List<ChatMessage> { new(ChatRole.User, trigger.Payload) };

        await using var run = await InProcessExecution.RunStreamingAsync(squadWorkflow, messages, cancellationToken: ct);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

        List<ChatMessage> finalHistory = [];
        await foreach (var evt in run.WatchStreamAsync(ct).ConfigureAwait(false).WithCancellation(ct))
        {
            if (evt is AgentResponseUpdateEvent e)
            {
                await nats.StreamAgentUpdateAsync(trigger.TriggerType, e.Update.AuthorName ?? "Squad", e.Update.Text, ct);
            }
            else if (evt is WorkflowOutputEvent outputEvt)
            {
                finalHistory = outputEvt.As<List<ChatMessage>>()!;
            }
        }

        var validator = await factory.CreateAsync(AgentRole.Validator, ct: ct);
        var validatorSession = await validator.CreateSessionAsync(ct);

        var squadFindings = string.Join("\n", finalHistory.Select(m => $"{m.Role}: {m.Text}"));
        var validationInput = $"Original Request: {trigger.Payload}\n\nSquad Findings:\n{squadFindings}";
        var finalFullResponse = new StringBuilder();

        await foreach (var update in validator.RunStreamingAsync(validationInput, validatorSession, cancellationToken: ct))
        {
            finalFullResponse.Append(update.Text);
            await nats.StreamAgentUpdateAsync(trigger.TriggerType, AgentRole.Validator, update.Text, ct);
        }

        return LlmParser.ParseWorkflowResult(finalFullResponse.ToString(), rolesToExecute);
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
