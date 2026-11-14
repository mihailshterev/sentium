namespace Sentium.Praxis.Features.Workflows.Squad;

/// <summary>
/// Instructs the step executor to run (or re-run) one step.
/// </summary>
internal sealed record StepRequest(SquadState State, int StepIndex, int Attempt, IReadOnlyList<string> Feedback, string? PreviousOutput)
{
    public SquadStep Step => State.Plan.Steps[StepIndex];
}
