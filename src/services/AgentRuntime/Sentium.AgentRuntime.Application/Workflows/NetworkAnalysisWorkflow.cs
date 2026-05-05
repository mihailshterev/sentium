using Sentium.AgentRuntime.Application.Common.Helpers;
using Sentium.AgentRuntime.Application.Extensions;
using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Workflows;
using Sentium.Infrastructure.Messaging;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Sentium.AgentRuntime.Application.Workflows;

public sealed class NetworkAnalysisWorkflow(IAgentFactory factory, IEventBus nats) : IAgentWorkflow
{
    public WorkflowType Type => WorkflowType.Predefined;

    public async Task<WorkflowResult> ExecuteAsync(WorkflowTrigger trigger, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        var analyst = await factory.CreateAsync(AgentRole.SecurityAnalyst, ct: ct);
        var forensics = await factory.CreateAsync(AgentRole.Forensics, ct: ct);
        var intel = await factory.CreateAsync(AgentRole.ThreatIntel, ct: ct);
        var validator = await factory.CreateAsync(AgentRole.Validator, ct: ct);

        var workflow = AgentWorkflowBuilder.BuildSequential("deep-analysis", [analyst, forensics, intel, validator]).AsAIAgent();

        var session = await workflow.CreateSessionAsync(ct);
        var streamLog = new StreamLogAccumulator();

        await foreach (var update in workflow.RunStreamingAsync(trigger.Payload, session, cancellationToken: ct))
        {
            var author = update.AuthorName ?? "SecurityAnalyst";

            if (update.Contents.OfType<TextReasoningContent>().FirstOrDefault() is { } reasoning && !string.IsNullOrEmpty(reasoning.Text))
            {
                await nats.StreamAgentUpdateAsync(trigger.TriggerType, author, reasoning.Text, AgentUpdateTypes.Thought, ct);
                streamLog.Add(author, reasoning.Text, AgentUpdateTypes.Thought);
            }

            foreach (var call in update.Contents.OfType<FunctionCallContent>())
            {
                var toolLabel = $"Calling {call.Name}...";
                await nats.StreamAgentUpdateAsync(trigger.TriggerType, author, toolLabel, AgentUpdateTypes.Tool, ct);
                streamLog.Add(author, toolLabel, AgentUpdateTypes.Tool);
            }

            if (!string.IsNullOrEmpty(update.Text))
            {
                await nats.StreamAgentUpdateAsync(trigger.TriggerType, author, update.Text, ct);
                streamLog.Add(author, update.Text, AgentUpdateTypes.Message);
            }
        }

        return new WorkflowResult
        {
            Explanation = "The security analyst identified an anomaly and delegated to the Sentinel agent.",
            Risk = "",
            Recommendation = "Check firewall rules for the source IP.",
            History = new List<(string Action, string Result)>(),
            StreamLog = streamLog.Entries
        };
    }
}

public record AgentStreamUpdate(string Author, string Text, string Type = "message");
