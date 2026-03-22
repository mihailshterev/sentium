using AgentRuntime.Application.Common.Helpers;
using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Workflows;
using Infrastructure.Messaging;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using NATS.Client.Serializers.Json;
using System.Text;

namespace AgentRuntime.Application.Workflows;

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
            await StreamToNats(trigger.TriggerType, AgentRole.Planner, update.Text, ct);
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

        var squadWorkflow = AgentWorkflowBuilder.BuildSequential("dynamic-squad", squadAgents).AsAIAgent();
        var squadSession = await squadWorkflow.CreateSessionAsync(ct);

        var squadReport = new StringBuilder();

        await foreach (var update in squadWorkflow.RunStreamingAsync(trigger.Payload, squadSession, cancellationToken: ct))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                squadReport.AppendLine($"{update.AuthorName}: {update.Text}");
                await StreamToNats(trigger.TriggerType, update.AuthorName ?? "Squad", update.Text, ct);
            }
        }

        var validator = await factory.CreateAsync(AgentRole.Validator, ct: ct);
        var validatorSession = await validator.CreateSessionAsync(ct);

        var validationInput = $"Original Request: {trigger.Payload}\n\nSquad Findings:\n{squadReport}";
        var finalFullResponse = new StringBuilder();

        await foreach (var update in validator.RunStreamingAsync(validationInput, validatorSession, cancellationToken: ct))
        {
            finalFullResponse.Append(update.Text);
            await StreamToNats(trigger.TriggerType, "Validator", update.Text, ct);
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
            agentLines.AppendLine($"- {name}: Built-in agent.");
        }

        foreach (var agent in dbAgents)
        {
            agentLines.AppendLine($"- {agent.Name}: {agent.Description}");
        }

        return $"""
            You are an orchestration agent. Analyze the input and determine which specialized agents are required to resolve the issue.

            Available Agents:
            {agentLines.ToString().TrimEnd()}

            You MUST output strictly a JSON array of strings representing the required agent roles. Do not include markdown, explanations, or any other text.
            Example output: ["Forensics", "ThreatIntel"]
            """;
    }

    private async Task StreamToNats(string type, string author, string text, CancellationToken ct)
    {
        await nats.PublishAsync($"stream.{type}", new AgentStreamUpdate(author, text),
            serializer: NatsJsonSerializer<AgentStreamUpdate>.Default, ct: ct);
    }
}
