using Sentium.Probatio.Entities;

namespace Sentium.Probatio.Common;

internal static class EvalCheckRules
{
    public static bool IsQuality(EvalCheckType type)
        => type is EvalCheckType.Relevance or EvalCheckType.Coherence or EvalCheckType.Groundedness or EvalCheckType.Completeness;

    public static bool NeedsExpectedAnswer(EvalCheckType type)
        => type is EvalCheckType.ContainsExpectedAnswer or EvalCheckType.Completeness;

    public static bool NeedsExpectedToolCalls(EvalCheckType type)
        => type is EvalCheckType.ToolsCalled or EvalCheckType.ToolArgumentsMatch;

    /// <summary>
    /// Groundedness is judged against the case's canned tool results, the only facts the agent is given.
    /// </summary>
    public static bool NeedsCannedToolResults(EvalCheckType type) => type is EvalCheckType.Groundedness;
}
