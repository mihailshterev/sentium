using System.Text;

namespace Sentium.AgentRuntime.Application.Common.Helpers;

/// <summary>
/// Builds the per-agent collaboration directive appended to each squad agent's instructions. It scopes the agent
/// to its assigned slice of the request and forbids it from answering the whole thing, speaking for other agents,
/// or declaring overall completion - the failure modes that let one agent finish everything (so the rest bow out)
/// or, on a sliced re-run, impersonate the entire squad.
/// </summary>
public static class SquadCollaborationPrompt
{
    /// <param name="agentName">The agent this directive is for.</param>
    /// <param name="assignment">The agent's specific sub-task (Orchestrator-assigned for Discovery); may be empty,
    /// in which case the agent is scoped to its own role/specialization.</param>
    /// <param name="position">1-based position of this agent in the pipeline.</param>
    /// <param name="total">Total number of agents in the pipeline.</param>
    /// <param name="rosterNames">All squad agent names in execution order.</param>
    public static string Build(string agentName, string? assignment, int position, int total, IReadOnlyList<string> rosterNames)
    {
        ArgumentNullException.ThrowIfNull(rosterNames);

        var assignmentLine = string.IsNullOrWhiteSpace(assignment) ? "what your role (described above) covers" : assignment.Trim();

        if (total <= 1)
        {
            return
                "### TASK\n" +
                $"Produce a complete, direct answer to the user's request, focused on {assignmentLine}.\n" +
                "Output only the deliverable - no apologies, acknowledgements, or STATUS lines.";
        }

        var pipeline = string.Join(" -> ", rosterNames);
        var sb = new StringBuilder();
        sb.Append("### PIPELINE ROLE\n");
        sb.Append($"You are \"{agentName}\", step {position} of {total} in a multi-agent pipeline answering ONE user request.\n");
        sb.Append($"Pipeline order: {pipeline}.\n");
        sb.Append($"Your assignment: {assignmentLine}.\n");
        sb.Append("Rules:\n");
        sb.Append("- Do ONLY your assignment. Leave the other parts to the agents responsible for them.\n");
        sb.Append("- Build on the prior agents' contributions already in the conversation; do not repeat or rewrite them.\n");
        sb.Append("- Do NOT restate the whole request, speak for other agents, or declare the overall task complete - a later step finalizes.\n");
        sb.Append("- Output ONLY your contribution: no apologies, acknowledgements, or STATUS lines.\n");
        sb.Append("- If your assignment does not apply to this request, say so in one sentence and stop.");

        return sb.ToString();
    }
}
