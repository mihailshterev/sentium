using Sentium.Probatio.Entities;

namespace Sentium.Probatio.Features.Suites;

/// <summary>
/// Turns validated <see cref="EvalCaseInput"/>s into stored cases: trimmed text, blank optionals as
/// <see langword="null"/>, missing lists as empty and new cases given an id.
/// </summary>
internal static class EvalCaseNormalizer
{
    public static List<EvalCase> Normalize(IReadOnlyList<EvalCaseInput>? cases)
        => cases is null ? [] : [.. cases.Select(Normalize)];

    private static EvalCase Normalize(EvalCaseInput input)
    {
        return new EvalCase(
            input.Id ?? Guid.CreateVersion7(),
            Blank(input.Name),
            input.Prompt.Trim(),
            [.. (input.PriorTurns ?? []).Select(t => t with { Text = t.Text.Trim() })],
            Blank(input.ExpectedAnswer),
            [.. (input.ExpectedToolCalls ?? []).Select(c => c with { ToolName = c.ToolName.Trim(), Arguments = c.Arguments is { Count: > 0 } ? c.Arguments : null })],
            [.. (input.CannedToolResults ?? []).Select(r => r with { ToolName = r.ToolName.Trim() })],
            [.. input.Checks ?? []]);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
