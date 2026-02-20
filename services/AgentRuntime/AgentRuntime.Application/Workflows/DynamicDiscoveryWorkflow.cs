using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Workflows;
using Microsoft.Agents.AI.Workflows;
using NATS.Client.Serializers.Json;

namespace AgentRuntime.Application.Workflows;

public class DynamicDiscoveryWorkflow : IAgentWorkflow
{
    public WorkflowType Type => WorkflowType.Dynamic;
    private readonly IAgentFactory AgentFactory;
    private readonly IAgentRegistry AgentRegistry;

    public DynamicDiscoveryWorkflow(IAgentFactory factory, IAgentRegistry registry)
    {
        AgentFactory = factory;
        AgentRegistry = registry;
    }

    public async Task<WorkflowResult> ExecuteAsync(WorkflowTrigger trigger, CancellationToken ct)
    {
        // var planner = AgentFactory.Create(AgentRole.Planner, ct: ct);
        // var session = await planner.CreateSessionAsync(ct);

        // string planJson = "";
        // await foreach (var update in planner.RunStreamingAsync(trigger.Payload, session, ct))
        // {
        //     planJson += update.Text;
        //     await Nats.PublishAsync($"stream.{trigger.TriggerType}", new AgentStreamUpdate("Planner", update.Text), serializer: NatsJsonSerializer<AgentStreamUpdate>.Default, ct: ct);
        // }

        // var roles = ParseRoles(planJson); // Your logic to turn "Analyst" into AgentRole.SecurityAnalyst
        // var dynamicAgents = roles.Select(r => AgentFactory.Create(r, ct: ct)).ToArray();

        // if (dynamicAgents.Any())
        // {
        //     var dynamicWorkflow = AgentWorkflowBuilder.BuildConcurrent("dynamic-squad", dynamicAgents).AsAgent();
        //     var dynamicSession = await dynamicWorkflow.CreateSessionAsync(ct);

        //     await foreach (var update in dynamicWorkflow.RunStreamingAsync(trigger.Payload, dynamicSession, ct))
        //     {
        //         if (!string.IsNullOrEmpty(update.Text))
        //         {
        //             await Nats.PublishAsync($"stream.{trigger.TriggerType}",
        //                 new AgentStreamUpdate(update.AuthorName ?? "Agent", update.Text),
        //                 serializer: NatsJsonSerializer<AgentStreamUpdate>.Default, ct: ct);
        //         }
        //     }
        // }

        return new WorkflowResult { Explanation = "Dynamic plan executed." };
    }
}
