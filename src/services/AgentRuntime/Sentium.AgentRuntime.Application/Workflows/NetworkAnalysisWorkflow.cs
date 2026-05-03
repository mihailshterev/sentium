using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Workflows;
using Sentium.Infrastructure.Messaging;
using Microsoft.Agents.AI.Workflows;
using NATS.Client.Serializers.Json;

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

        await foreach (var update in workflow.RunStreamingAsync(trigger.Payload, session, cancellationToken: ct))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                var streamPayload = new AgentStreamUpdate(
                    update.AuthorName ?? "SecurityAnalyst",
                    update.Text
                );
                await nats.PublishAsync($"stream.{trigger.TriggerType}", streamPayload, serializer: NatsJsonSerializer<AgentStreamUpdate>.Default, ct: ct);
            }
        }

        return new WorkflowResult
        {
            Explanation = "The security analyst identified an anomaly and delegated to the Sentinel agent.",
            Risk = "",
            Recommendation = "Check firewall rules for the source IP.",
            History = new List<(string Action, string Result)>()
        };
    }
}

public record AgentStreamUpdate(string Author, string Text);
