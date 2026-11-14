namespace Sentium.Praxis.Features.Workflows.Squad;

/// <summary>
/// All steps finished; ready for synthesis.
/// </summary>
internal sealed record SquadOutcome(SquadState State);
