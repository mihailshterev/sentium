using Microsoft.Agents.AI;

namespace Sentium.Praxis.Features.Agents;

/// <summary>
/// Builds runnable <see cref="AIAgent"/> instances by name. Resolves native built-in agents first and falls back
/// to the current user's database agents.
/// </summary>
public interface IAgentFactory
{
    /// <summary>
    /// Resolves an agent by name and constructs a fully equipped instance: harness, curated tools, skills, learnings.
    /// </summary>
    /// <param name="agentName">The agent name - matched against native built-in agents first, then the current user's database agents.</param>
    /// <param name="overrideInstructions">Optional role instructions that replace the resolved agent's defaults.</param>
    /// <param name="overrideModel">Optional model id to run on instead of the configured default model.</param>
    /// <param name="actingUserId">The user on whose behalf the agent runs; flows into per-user settings and agent lookup.</param>
    /// <param name="chatHistory">Optional chat-history provider (e.g. a persisted conversation); defaults to in-memory session history.</param>
    /// <param name="allowApprovalTools">
    /// <see langword="false"/> for unattended runs (workflows): drops tools that require human approval, since nobody can answer the request.
    /// </param>
    /// <param name="ct">A cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="agentName"/> matches neither a native nor a database agent.</exception>
    Task<AIAgent> CreateAsync(
        string agentName,
        string? overrideInstructions = null,
        string? overrideModel = null,
        Guid? actingUserId = null,
        ChatHistoryProvider? chatHistory = null,
        bool allowApprovalTools = true,
        CancellationToken ct = default);

    /// <summary>
    /// Constructs a lean <em>control</em> agent for a structured decision (planning, reviewing, synthesizing): no harness,
    /// tools, skills, or learnings, and temperature 0. Pair runs with a JSON-schema response format so small models are
    /// constrained to a valid answer.
    /// </summary>
    /// <param name="agentName">A native agent name (e.g. <see cref="AgentRole.Orchestrator"/>, <see cref="AgentRole.Validator"/>).</param>
    /// <param name="overrideInstructions">Optional instructions that replace the native agent's defaults.</param>
    /// <param name="overrideModel">Optional model id.</param>
    /// <param name="actingUserId">The user on whose behalf the agent runs.</param>
    /// <param name="ct">A cancellation token.</param>
    Task<AIAgent> CreateControlAgentAsync(
        string agentName,
        string? overrideInstructions = null,
        string? overrideModel = null,
        Guid? actingUserId = null,
        CancellationToken ct = default);
}
