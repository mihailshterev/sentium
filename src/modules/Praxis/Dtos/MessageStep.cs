namespace Sentium.Praxis.Dtos;

/// <summary>
/// One entry of an assistant turn's activity timeline, in the order it happened.
/// </summary>
/// <param name="Kind"><c>thought</c> (reasoning), <c>text</c> (narration before a later tool call), or <c>tool</c>.</param>
/// <param name="Text">Reasoning or narration text (thought/text steps).</param>
/// <param name="CallId">The tool call id (tool steps).</param>
/// <param name="Name">The tool name (tool steps).</param>
/// <param name="Arguments">The call's arguments as JSON, capped (tool steps).</param>
/// <param name="Status">Tool outcome: <c>ok</c>, <c>error</c>, <c>denied</c> (by policy), <c>rejected</c> (by the user), <c>running</c>, or <c>awaiting_approval</c>.</param>
/// <param name="Result">A capped preview of the tool's result (tool steps).</param>
/// <param name="DurationMs">How long the tool ran, when measured.</param>
public sealed record MessageStep(
    string Kind,
    string? Text = null,
    string? CallId = null,
    string? Name = null,
    string? Arguments = null,
    string? Status = null,
    string? Result = null,
    long? DurationMs = null);
