using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Dtos;
using AgentRuntime.Core.Workflows;
using Infrastructure.Messaging;
using Microsoft.Agents.AI.Workflows;
using NATS.Client.Serializers.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AgentRuntime.Application.Workflows;

public partial class DynamicDiscoveryWorkflow(
    IAgentFactory factory,
    IAgentRegistry registry,
    IAgentManager agentManager,
    IEventBus nats) : IAgentWorkflow
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public WorkflowType Type => WorkflowType.Dynamic;

    public async Task<WorkflowResult> ExecuteAsync(WorkflowTrigger trigger, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        var dbAgents = await agentManager.GetAgentsAsync(ct);
        var dbAgentMap = dbAgents.ToDictionary(a => a.Name, a => a.Description, StringComparer.OrdinalIgnoreCase);

        var plannerInstructions = BuildPlannerInstructions(dbAgents);

        var planner = factory.Create(AgentRole.Planner, overrideInstructions: plannerInstructions, ct: ct);
        var plannerSession = await planner.CreateSessionAsync(ct);

        var rawPlan = string.Empty;
        await foreach (var update in planner.RunStreamingAsync(trigger.Payload, plannerSession, cancellationToken: ct))
        {
            rawPlan += update.Text;
            await StreamToNats(trigger.TriggerType, AgentRole.Planner, update.Text, ct);
        }

        var rolesToExecute = ParseRolesRobustly(rawPlan, dbAgentMap);
        if (rolesToExecute.Count == 0)
        {
            return new WorkflowResult { Explanation = "Planner failed to identify required agents." };
        }

        var squadAgents = rolesToExecute
            .Select(role =>
            {
                var overrideInstructions = dbAgentMap.TryGetValue(role, out var desc) ? desc : null;
                return factory.Create(role, overrideInstructions: overrideInstructions, ct: ct);
            })
            .ToArray();

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

        var validator = factory.Create(AgentRole.Validator, ct: ct);
        var validatorSession = await validator.CreateSessionAsync(ct);

        var validationInput = $"Original Request: {trigger.Payload}\n\nSquad Findings:\n{squadReport}";
        var finalFullResponse = new StringBuilder();

        await foreach (var update in validator.RunStreamingAsync(validationInput, validatorSession, cancellationToken: ct))
        {
            finalFullResponse.Append(update.Text);
            await StreamToNats(trigger.TriggerType, "Validator", update.Text, ct);
        }

        return ParseFinalResult(finalFullResponse.ToString(), rolesToExecute);
    }

    private string BuildPlannerInstructions(IReadOnlyList<AgentResponse> dbAgents)
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

    private static WorkflowResult ParseFinalResult(string validatorOutput, List<string> roles)
    {
        var riskMatch = Regex.Match(validatorOutput, @"RISK:\s*(.*)", RegexOptions.IgnoreCase);
        var recMatch = Regex.Match(validatorOutput, @"RECOMMENDATION:\s*(.*)", RegexOptions.IgnoreCase);

        return new WorkflowResult
        {
            Explanation = validatorOutput,
            Risk = riskMatch.Groups[1].Value.Trim() is { Length: > 0 } r ? r : "Unknown",
            Recommendation = recMatch.Groups[1].Value.Trim() is { Length: > 0 } rec ? rec : "Review squad logs manually.",
            History = roles.Select(r => ("AgentSelection", r)).ToList()
        };
    }

    private List<string> ParseRolesRobustly(string llmOutput, Dictionary<string, string> dbAgentMap)
    {
        try
        {
            var cleanJson = Regex.Replace(llmOutput, @"```(?:json)?\s*([\s\S]*?)\s*```", "$1").Trim();
            var match = JsonArrayRegex().Match(cleanJson);
            if (!match.Success)
            {
                return [];
            }

            var parsed = JsonSerializer.Deserialize<List<string>>(match.Value, JsonOptions);
            if (parsed is null)
            {
                return [];
            }

            var registeredNames = registry.GetRegisteredNames().ToList();

            var resolved = new List<string>();
            foreach (var p in parsed)
            {
                var dbMatch = dbAgentMap.Keys.FirstOrDefault(k =>
                    string.Equals(k.Replace(" ", ""), p.Replace(" ", ""), StringComparison.OrdinalIgnoreCase));

                if (dbMatch is not null)
                {
                    if (!resolved.Contains(dbMatch, StringComparer.OrdinalIgnoreCase))
                    {
                        resolved.Add(dbMatch);
                    }

                    continue;
                }

                var registryMatch = registeredNames.FirstOrDefault(r =>
                    string.Equals(r.Replace(" ", ""), p.Replace(" ", ""), StringComparison.OrdinalIgnoreCase));

                if (registryMatch is not null && !resolved.Contains(registryMatch, StringComparer.OrdinalIgnoreCase))
                {
                    resolved.Add(registryMatch);
                }
            }

            return resolved;
        }
        catch
        {
            return [];
        }
    }

    private async Task StreamToNats(string type, string author, string text, CancellationToken ct)
    {
        await nats.PublishAsync($"stream.{type}", new AgentStreamUpdate(author, text),
            serializer: NatsJsonSerializer<AgentStreamUpdate>.Default, ct: ct);
    }

    [GeneratedRegex(@"\[\s*.*?\s*\]", RegexOptions.Singleline)]
    private static partial Regex JsonArrayRegex();
}
