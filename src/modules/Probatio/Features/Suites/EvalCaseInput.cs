using Sentium.Probatio.Entities;

namespace Sentium.Probatio.Features.Suites;

/// <summary>
/// An Eval Case as authored. A case without <paramref name="Id"/> is new and gets one; a case that keeps its id
/// across edits is the same case in the next version.
/// </summary>
internal sealed record EvalCaseInput(
    Guid? Id,
    string? Name,
    string Prompt,
    IReadOnlyList<EvalTurn>? PriorTurns,
    string? ExpectedAnswer,
    IReadOnlyList<ExpectedToolCall>? ExpectedToolCalls,
    IReadOnlyList<CannedToolResult>? CannedToolResults,
    IReadOnlyList<EvalCheck>? Checks);
