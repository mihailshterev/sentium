using Microsoft.Agents.AI;

namespace Sentium.AgentRuntime.Core.Agents;

/// <summary>
/// Builds runnable <see cref="AIAgent"/> instances by name. Resolves native built-in agents first and falls back
/// to the current user's database agents, then wraps the result with the model harness, tools, and skills.
/// </summary>
public interface IAgentFactory
{
    /// <summary>
    /// Resolves an agent by name and constructs a configured instance ready to run.
    /// </summary>
    /// <param name="agentName">The agent name - matched against native built-in agents first, then the current user's database agents.</param>
    /// <param name="overrideInstructions">Optional system instructions that replace the resolved agent's defaults (typically used for database-defined agents).</param>
    /// <param name="overrideModel">Optional model id to run on instead of the configured default model.</param>
    /// <param name="actingUserId">The user on whose behalf the agent runs; flows into per-user scoping and policy decisions.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A configured <see cref="AIAgent"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="agentName"/> matches neither a native nor a database agent.</exception>
    Task<AIAgent> CreateAsync(string agentName, string? overrideInstructions = null, string? overrideModel = null, Guid? actingUserId = null, CancellationToken ct = default);
}
