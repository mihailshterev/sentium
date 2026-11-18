namespace Sentium.Praxis.Contracts;

/// <summary>
/// Answers a tool call made during an Evaluation in place of the tool (ADR 0003). The returned text is what the
/// model sees as the tool's result.
/// </summary>
public delegate ValueTask<string> EvaluationToolStub(string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken ct);
