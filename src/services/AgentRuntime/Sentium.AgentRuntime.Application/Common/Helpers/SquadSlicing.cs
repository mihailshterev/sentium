namespace Sentium.AgentRuntime.Application.Common.Helpers;

/// <summary>
/// Helpers for the refinement loop's "squad slicing" - deciding which agents to re-run after the
/// Validator blames specific agent(s). Because the squad executes sequentially (A -> B -> C), each
/// agent consumes the previous agent's output. Re-running only a flagged middle agent would therefore
/// leave every downstream agent's output stale and inconsistent with the correction, so slicing must
/// re-flow the pipeline from the earliest flagged agent through the end.
/// </summary>
public static class SquadSlicing
{
    /// <summary>
    /// Returns the index of the earliest agent in <paramref name="squadNames"/> that appears in
    /// <paramref name="flaggedNames"/> (case-insensitive). Re-running from this index onward re-flows
    /// the sequential pipeline so downstream agents reprocess the corrected upstream output.
    /// Returns <c>-1</c> when nothing is flagged, signalling the caller to re-run the full squad.
    /// </summary>
    public static int ComputeReflowStartIndex(IReadOnlyList<string> squadNames, IReadOnlyList<string> flaggedNames)
    {
        ArgumentNullException.ThrowIfNull(squadNames);
        ArgumentNullException.ThrowIfNull(flaggedNames);

        if (flaggedNames.Count == 0 || squadNames.Count == 0)
        {
            return -1;
        }

        var flagged = new HashSet<string>(flaggedNames, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < squadNames.Count; i++)
        {
            if (flagged.Contains(squadNames[i]))
            {
                return i;
            }
        }

        return -1;
    }
}
