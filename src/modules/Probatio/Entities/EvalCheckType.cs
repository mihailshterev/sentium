using System.Text.Json.Serialization;

namespace Sentium.Probatio.Entities;

/// <summary>
/// What a Check measures. Deterministic Checks read the case's expected answer or expected tool calls; quality
/// Checks are scored 1-5 by the Judge.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<EvalCheckType>))]
internal enum EvalCheckType
{
    /// <summary>
    /// The answer contains the case's expected answer (case-insensitive).
    /// </summary>
    ContainsExpectedAnswer,

    /// <summary>
    /// Every expected tool was called, by name.
    /// </summary>
    ToolsCalled,

    /// <summary>
    /// Every expected tool call was made with at least its expected arguments.
    /// </summary>
    ToolArgumentsMatch,

    Relevance,
    Coherence,
    Groundedness,

    /// <summary>
    /// How fully the answer covers the case's expected answer.
    /// </summary>
    Completeness,
}
