using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Workflows;

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
        var availablePersonas = AgentRegistry.GetRegisteredNames();

        var planner = AgentFactory.Create(AgentRole.Planner,
            $"You are an orchestrator. Available agents: {string.Join(", ", availablePersonas)}. " +
            "Based on the input, delegate tasks to the appropriate agents.", ct
        );

        var thread = await planner.CreateSessionAsync(ct);

        var result = await planner.RunAsync(trigger.Payload, thread, cancellationToken: ct);

        // TODO: Parse the planner's response to determine next steps
        return new WorkflowResult
        {
            Explanation = "The planner identified an anomaly and delegated to the Sentinel agent.",
            Risk = "",
            Recommendation = "Check firewall rules for the source IP.",
            History = result.Messages.Select(m => new { m.Role, m.Text }).ToList()
        };
    }
}
