using Sentium.AgentRuntime.Application.Common.Helpers;
using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Workflows;
using Sentium.Infrastructure.Messaging;
using Microsoft.Agents.AI;

namespace Sentium.AgentRuntime.Application.Workflows;

public sealed class DynamicDiscoveryWorkflow(
    IAgentFactory factory,
    IAgentRegistry registry,
    IAgentRepository agentRepository,
    IEventBus nats) : IAgentWorkflow
{
    public WorkflowType Type => WorkflowType.Dynamic;

    public async Task<WorkflowResult> ExecuteAsync(WorkflowTrigger trigger, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        var workspaceContext = WorkspaceContextPrompt.TryExtract(trigger.Payload);

        var dbAgents = await agentRepository.GetAgentsForUserAsync(trigger.UserId, ct);
        var dbAgentMap = dbAgents.ToDictionary(a => a.Name, a => a.Description, StringComparer.OrdinalIgnoreCase);
        var dbAgentModelMap = dbAgents.ToDictionary(a => a.Name, a => a.Model, StringComparer.OrdinalIgnoreCase);

        var orchestrator = await factory.CreateAsync(AgentRole.Orchestrator, actingUserId: trigger.UserId, ct: ct);
        var orchestratorSession = await orchestrator.CreateSessionAsync(ct);

        var streamLog = new StreamLogAccumulator();
        var input = WorkspaceContextPrompt.Augment(trigger.Payload, workspaceContext);

        var rawPlan = await AgentTurnStreamer.RunAsync(orchestrator, input, orchestratorSession, trigger, AgentRole.Orchestrator, nats, streamLog, ct);

        var assignments = LlmParser.ParseAgentAssignments(rawPlan, dbAgentMap, registry);
        if (assignments.Count == 0)
        {
            return new WorkflowResult { Explanation = "Orchestrator failed to identify required agents.", StreamLog = streamLog.Entries, UserId = trigger.UserId };
        }

        var rosterNames = assignments.Select(a => a.Agent).ToList();

        var squadAgents = new List<AIAgent>();
        var roster = new List<SquadMember>();

        for (var i = 0; i < assignments.Count; i++)
        {
            var (role, task) = assignments[i];
            var baseInstructions = dbAgentMap.TryGetValue(role, out var desc) ? desc : registry.GetInstructions(role);
            var overrideModel = dbAgentModelMap.TryGetValue(role, out var mdl) && !string.IsNullOrWhiteSpace(mdl) ? mdl : null;

            var directive = SquadCollaborationPrompt.Build(role, task, i + 1, assignments.Count, rosterNames);
            var instructions = $"{baseInstructions}\n\n{directive}";

            var agent = await factory.CreateAsync(role, overrideInstructions: instructions, overrideModel: overrideModel, actingUserId: trigger.UserId, ct: ct);
            squadAgents.Add(agent);

            roster.Add(new SquadMember(role, baseInstructions));
        }

        var loop = new AgenticRefinementLoop(factory, nats);
        var outcome = await loop.RunAsync(trigger, "dynamic-squad", squadAgents, input, streamLog, ct, roster);

        return LlmParser.ParseWorkflowResult(outcome.ValidatorOutput, outcome.SquadText, rosterNames, streamLog.Entries, trigger.UserId);
    }
}
