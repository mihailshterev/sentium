namespace Sentium.Probatio.Entities;

/// <summary>
/// One repetition of one Eval Case. <paramref name="Error"/> is set when the agent could not answer (the
/// repetition then fails); token counts are <see langword="null"/> when the model did not report usage.
/// </summary>
internal sealed record EvaluationRepetition(
    int Number,
    bool Passed,
    IReadOnlyList<TranscriptMessage> Transcript,
    IReadOnlyList<RecordedToolCall> ToolCalls,
    IReadOnlyList<CheckOutcome> Checks,
    long LatencyMs,
    long? InputTokens,
    long? OutputTokens,
    double? TokensPerSecond,
    string? Error);
