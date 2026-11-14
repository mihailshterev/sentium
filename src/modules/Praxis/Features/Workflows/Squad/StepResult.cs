namespace Sentium.Praxis.Features.Workflows.Squad;

/// <summary>
/// A finished step. <see cref="Verified"/> is <see langword="false"/> when the output was accepted without passing review
/// (retry budget exhausted or the model stopped changing its answer).
/// </summary>
public sealed record StepResult(SquadStep Step, string Output, bool Verified);
