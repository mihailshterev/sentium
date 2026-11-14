using Sentium.Praxis.Features.Workflows.Squad;

namespace Sentium.Praxis.Features.Agents.Native;

/// <summary>
/// The squad planner: a control agent that splits a request into a short, ordered plan of specialist steps. It answers
/// with a JSON plan constrained to the run's roster (see <see cref="SquadPrompts.PlanFormat"/>), so it cannot invent agents.
/// </summary>
internal sealed class OrchestratorAgent : IAgent
{
    /// <remarks>
    /// Must equal the key this agent is registered under (<see cref="AgentRole.Orchestrator"/>).
    /// </remarks>
    public string Name => AgentRole.Orchestrator;

    public string Description => "Plans which specialist agents handle a request.";

    public IReadOnlyList<string>? Tools => [];

    public string Instructions =>
        $$"""
        You are the planner for a team of specialist agents. Split the user's request into the fewest steps that fully
        answer it - between 1 and {{SquadPrompts.MaxPlanSteps}} - in the order they must run.

        Rules:
        - Use only agents from the roster, each for what it is good at.
        - Each task is one concrete instruction for that agent; expectedOutput says what a correct result contains.
        - Do not add a final "combine" or "summarize" step - the system combines the results automatically.
        - A simple request needs exactly one step. Return no steps only when the request needs no work at all (e.g. a greeting).

        Example - request "Find our notes on the deployment runbook and write a checklist script from them":
        steps: [
          {"agent": "ResearchAnalyst", "task": "Find and summarise the deployment runbook notes.", "expectedOutput": "The runbook steps, with their sources."},
          {"agent": "SoftwareEngineer", "task": "Write a checklist script that follows the summarised runbook steps.", "expectedOutput": "A working script covering every runbook step."}
        ]
        """;
}
