using System.Text;
using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Dtos;

namespace Sentium.AgentRuntime.Application.Common.Helpers;

/// <summary>
/// Builds the instructions for the native Orchestrator agent. The Orchestrator not only selects which agents run
/// (and in what order) but assigns each one a concrete sub-task, so the squad divides the work instead of a single
/// agent answering the whole request. The available-agents roster is injected at runtime so it never goes stale.
/// </summary>
public static class OrchestratorTemplate
{
    public const string SystemRole = """
        ### Role
        You are an Orchestration Director. Decompose ONE user request into a sequential pipeline of specialized
        agents, and assign EACH agent the specific sub-task it must perform.

        ### Rules
        1. **Order Matters**: list agents in the exact order they should execute; each builds on the previous output.
        2. **Divide The Work**: give every agent a distinct sub-task scoped to its specialization. NEVER hand one
           agent the entire request. If the request needs combining/summarizing, make the LAST agent responsible for
           synthesizing the others' work.
        3. **Efficiency**: use only the agents strictly required, and never list the same agent twice.
        4. **Output Format**: output ONLY a valid JSON array of objects, each
           {"agent": "<exact name from Available Agents>", "task": "<one concise sentence telling that agent exactly what to produce>"}.
        5. **No Prose**: no explanations, no thinking, no markdown fences like ```json.

        ### Examples
        - Request: "Compare X in Python and Go, then summarize."
          Output: [{"agent":"AgentA","task":"Produce the Python portion."},{"agent":"AgentB","task":"Produce the Go portion."},{"agent":"AgentC","task":"Synthesize both portions into one comparison."}]
        - Request: "Hello, how are you?"
          Output: []
        """;

    public static string Build(string availableAgents) => $"""
        {SystemRole}

        ### Available Agents
        {availableAgents}

        ### Final Decision
        Output the JSON array of agent/task assignments for the current request:
        """;

    public static string Build(IAgentRegistry registry, IReadOnlyList<AgentResponse> dbAgents)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(dbAgents);

        var sb = new StringBuilder();

        var builtIn = registry.GetRegisteredNames()
            .Where(n => !n.Equals(AgentRole.Orchestrator, StringComparison.OrdinalIgnoreCase)
                     && !n.Equals(AgentRole.Validator, StringComparison.OrdinalIgnoreCase));

        foreach (var name in builtIn)
        {
            sb.AppendLine($"- {name}: {registry.GetInstructions(name)}");
        }

        foreach (var agent in dbAgents)
        {
            sb.AppendLine($"- {agent.Name}: {agent.Description}");
        }

        return Build(sb.ToString());
    }
}
