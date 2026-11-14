using System.ComponentModel;

namespace Sentium.Praxis.Features.Workflows.Squad;

/// <summary>
/// Structured assessment of the final answer (constrained decoding).
/// </summary>
public sealed record FinalAssessment(
    [property: Description("Risk of acting on the answer, considering accuracy and completeness.")] RiskLevel Risk,
    [property: Description("One-sentence recommendation for the user.")] string Recommendation);
