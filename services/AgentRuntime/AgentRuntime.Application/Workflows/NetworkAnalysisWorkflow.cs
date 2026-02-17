using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Workflows;
using Infrastructure.Messaging;
using Microsoft.Agents.AI.Workflows;
using NATS.Client.Serializers.Json;

namespace AgentRuntime.Application.Workflows;

public sealed class NetworkAnalysisWorkflow : IAgentWorkflow
{
    public WorkflowType Type => WorkflowType.Predefined;

    private readonly IAgentFactory AgentFactory;
    private readonly IAgentRegistry AgentRegistry;
    private readonly IEventBus Nats;

    public NetworkAnalysisWorkflow(IAgentFactory factory, IAgentRegistry registry, IEventBus nats)
    {
        AgentFactory = factory;
        AgentRegistry = registry;
        Nats = nats;
    }

    public async Task<WorkflowResult> ExecuteAsync(WorkflowTrigger trigger, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        var availablePersonas = AgentRegistry.GetRegisteredNames();

        var analyst = AgentFactory.Create(AgentRole.SecurityAnalyst, ct: ct);
        var forensics = AgentFactory.Create(AgentRole.Forensics, ct: ct);
        var intel = AgentFactory.Create(AgentRole.ThreatIntel, ct: ct);
        var summarizer = AgentFactory.Create(AgentRole.Summarizer, ct: ct);

        var workflow = AgentWorkflowBuilder.BuildSequential("deep-analysis", [analyst, forensics, intel, summarizer]).AsAgent();

        // var workflow = AgentWorkflowBuilder.BuildSequential("full-pipeline", [
        //     analysisGroup.AsAgent(),
        //     summarizer
        // ]).AsAgent();

        var session = await workflow.CreateSessionAsync(ct);

        await foreach (var update in workflow.RunStreamingAsync(trigger.Payload, session, cancellationToken: ct))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                var streamPayload = new AgentStreamUpdate(
                    update.AuthorName ?? "SecurityAnalyst",
                    update.Text
                );
                await Nats.PublishAsync($"stream.{trigger.TriggerType}", streamPayload, serializer: NatsJsonSerializer<AgentStreamUpdate>.Default, ct: ct);
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
