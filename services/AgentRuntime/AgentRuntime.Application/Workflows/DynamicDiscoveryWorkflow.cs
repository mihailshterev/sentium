using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Workflows;
using Infrastructure.Messaging;
using Microsoft.Agents.AI.Workflows;
using NATS.Client.Serializers.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AgentRuntime.Application.Workflows;

public partial class DynamicDiscoveryWorkflow : IAgentWorkflow
{
    public WorkflowType Type => WorkflowType.Dynamic;
    private readonly IAgentFactory AgentFactory;
    private readonly IAgentRegistry AgentRegistry;
    private readonly IEventBus Nats;

    public DynamicDiscoveryWorkflow(IAgentFactory factory, IAgentRegistry registry, IEventBus nats)
    {
        AgentFactory = factory;
        AgentRegistry = registry;
        Nats = nats;
    }

    public async Task<WorkflowResult> ExecuteAsync(WorkflowTrigger trigger, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        var planner = AgentFactory.Create(AgentRole.Planner, ct: ct);
        var plannerSession = await planner.CreateSessionAsync(ct);

        var rawPlan = string.Empty;
        await foreach (var update in planner.RunStreamingAsync(trigger.Payload, plannerSession, cancellationToken: ct))
        {
            rawPlan += update.Text;
            await StreamToNats(trigger.TriggerType, "Planner", update.Text, ct);
        }

        var rolesToExecute = ParseRolesRobustly(rawPlan);
        if (rolesToExecute.Count == 0)
        {
            return new WorkflowResult { Explanation = "Planner failed to identify required agents." };
        }

        var squadAgents = rolesToExecute.Select(role => AgentFactory.Create(role, ct: ct)).ToArray();
        var squadWorkflow = AgentWorkflowBuilder.BuildSequential("dynamic-squad", squadAgents).AsAgent();
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

        var validator = AgentFactory.Create(AgentRole.Validator, ct: ct);
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

    private static WorkflowResult ParseFinalResult(string validatorOutput, List<string> roles)
    {
        var riskMatch = Regex.Match(validatorOutput, @"RISK:\s*(.*)", RegexOptions.IgnoreCase);
        var recMatch = Regex.Match(validatorOutput, @"RECOMMENDATION:\s*(.*)", RegexOptions.IgnoreCase);

        return new WorkflowResult
        {
            Explanation = validatorOutput, // Full summary
            Risk = riskMatch.Groups[1].Value.Trim() ?? "Unknown",
            Recommendation = recMatch.Groups[1].Value.Trim() ?? "Review squad logs manually.",
            History = roles.Select(r => ("AgentSelection", r)).ToList()
        };
    }

    private List<string> ParseRolesRobustly(string llmOutput)
    {
        try
        {
            var cleanJson = Regex.Replace(llmOutput, @"```(?:json)?\s*([\s\S]*?)\s*```", "$1").Trim();
            var match = JsonArrayRegex().Match(cleanJson);
            if (!match.Success)
            {
                return [];
            }

            var parsed = JsonSerializer.Deserialize<List<string>>(match.Value, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var registered = AgentRegistry.GetRegisteredNames();

            return parsed?.Select(p => registered.FirstOrDefault(r =>
                string.Equals(r.Replace(" ", ""), p.Replace(" ", ""), StringComparison.OrdinalIgnoreCase)))
                .Where(m => m != null).Cast<string>().Distinct().ToList() ?? new();
        }
        catch
        {
            return [];
        }
    }

    private async Task StreamToNats(string type, string author, string text, CancellationToken ct)
    {
        await Nats.PublishAsync($"stream.{type}", new AgentStreamUpdate(author, text),
            serializer: NatsJsonSerializer<AgentStreamUpdate>.Default, ct: ct);
    }

    [GeneratedRegex(@"\[\s*.*?\s*\]", RegexOptions.Singleline)]
    private static partial Regex JsonArrayRegex();
}
