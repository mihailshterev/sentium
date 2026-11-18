namespace Sentium.Probatio.Entities;

/// <summary>
/// One Eval Case as stored in a suite version. <see cref="Id"/> stays the same across versions while the case is
/// kept, so results can be followed per case.
/// </summary>
internal sealed record EvalCase(
    Guid Id,
    string? Name,
    string Prompt,
    IReadOnlyList<EvalTurn> PriorTurns,
    string? ExpectedAnswer,
    IReadOnlyList<ExpectedToolCall> ExpectedToolCalls,
    IReadOnlyList<CannedToolResult> CannedToolResults,
    IReadOnlyList<EvalCheck> Checks);
