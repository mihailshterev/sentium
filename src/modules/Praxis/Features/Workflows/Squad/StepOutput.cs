namespace Sentium.Praxis.Features.Workflows.Squad;

/// <summary>
/// A step's raw output, on its way to review.
/// </summary>
internal sealed record StepOutput(StepRequest Request, string Output);
