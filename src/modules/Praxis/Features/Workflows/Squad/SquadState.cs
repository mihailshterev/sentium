namespace Sentium.Praxis.Features.Workflows.Squad;

/// <summary>
/// Run state threaded through the graph. Workflow messages are immutable, so the graph carries all run state and
/// executors stay stateless.
/// </summary>
internal sealed record SquadState(string Request, SquadPlan Plan, IReadOnlyList<StepResult> Completed);
