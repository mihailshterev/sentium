using System.ComponentModel;

namespace Sentium.Praxis.Features.Workflows.Squad;

/// <summary>
/// Structured verdict for a single step (constrained decoding).
/// </summary>
public sealed record StepVerdict(
    [property: Description("True when the output accomplishes the task and contains what the expected output describes.")] bool Passed,
    [property: Description("When not passed: at most three short, specific fixes. Empty when passed.")] IReadOnlyList<string> Issues);
