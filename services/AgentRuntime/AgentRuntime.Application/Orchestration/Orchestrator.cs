using System.Text.Json;
using AgentRuntime.Core.Agents;
using Microsoft.Agents.AI;

namespace AgentRuntime.Application.Orchestration;

public sealed class Orchestrator
{
    private readonly ChatClientAgent PlannerAgent;

    public Orchestrator(ChatClientAgent plannerAgent)
    {
        PlannerAgent = plannerAgent;
    }

    public async Task<IReadOnlyList<AgentTask>> PlanAsync(
        string input,
        CancellationToken ct)
    {
        var thread = await PlannerAgent.GetNewThreadAsync(ct);
        var result = await PlannerAgent.RunAsync(input, thread, cancellationToken: ct);

        var json = result.Messages.Last().Text;

        var plan = JsonSerializer.Deserialize<PlanResult>(json)
                   ?? throw new InvalidOperationException("Invalid plan");

        return plan.Tasks
            .Select(t => new AgentTask(
                Enum.Parse<AgentRole>(t.Role),
                t.Instruction))
            .ToList();
    }

    private sealed record PlanResult(List<TaskDto> Tasks);
    private sealed record TaskDto(string Role, string Instruction);
}
