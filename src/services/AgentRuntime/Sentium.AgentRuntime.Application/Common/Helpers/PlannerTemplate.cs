using System.Text;
using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Dtos;

namespace Sentium.AgentRuntime.Application.Common.Helpers;

public static class PlannerTemplate
{
    public const string SystemRole = """
        ### Role
        You are an Orchestration Planner. Your task is to decompose a user request into a sequential pipeline of specialized agents.

        ### Rules
        1. **Order Matters**: List agents in the exact order they should execute.
        2. **Efficiency**: Use only the agents strictly required to solve the request.
        3. **Output Format**: You must output ONLY a valid JSON array of strings (e.g., ["Agent1", "Agent2"]).
        4. **No Prose**: Do not include explanations, thinking process, or markdown formatting tags like ```json.

        ### Examples
        - Request: "Analyze and summarize this report."
          Output: ["AgentA", "AgentB"]
        - Request: "Hello, how are you?"
          Output: []
        """;

    public static string Build(string availableAgents) => $"""
        {SystemRole}

        ### Available Agents
        {availableAgents}

        ### Final Decision
        Output the JSON array for the current request:
        """;

    public static string Build(IAgentRegistry registry, IReadOnlyList<AgentResponse> dbAgents)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(dbAgents);

        var sb = new StringBuilder();

        var builtIn = registry.GetRegisteredNames()
            .Where(n => !n.Equals(AgentRole.Planner, StringComparison.OrdinalIgnoreCase)
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
