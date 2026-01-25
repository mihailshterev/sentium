using AgentRuntime.Application.Agents;
using AgentRuntime.Application.Orchestration;

namespace AgentRuntime.Application.Workflow;

public sealed class DynamicWorkflowExecutor
{
    private readonly Orchestrator _orchestrator;
    private readonly IAgentFactory _agentFactory;

    public DynamicWorkflowExecutor(
        Orchestrator orchestrator,
        IAgentFactory agentFactory)
    {
        _orchestrator = orchestrator;
        _agentFactory = agentFactory;
    }

    public async Task<IReadOnlyList<string>> ExecuteAsync(
        string input,
        CancellationToken ct)
    {
        var tasks = await _orchestrator.PlanAsync(input, ct);

        var results = new List<string>();
        var currentInput = input;

        foreach (var task in tasks)
        {
            var agent = _agentFactory.CreateAgent(task.Role);
            var thread = await agent.GetNewThreadAsync(ct);

            var response = await agent.RunAsync(
                $"{task.Instruction}\n\n{currentInput}",
                thread,
                cancellationToken: ct);

            var output = response.Messages.Last().Text;

            results.Add(output);
            currentInput = output;
        }

        return results;
    }
}
