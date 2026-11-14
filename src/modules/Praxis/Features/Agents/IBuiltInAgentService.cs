using Sentium.Core.Results;

namespace Sentium.Praxis.Features.Agents;

/// <summary>
/// Built-in (non-control) agents and the signed-in user's tool overrides of them.
/// </summary>
public interface IBuiltInAgentService
{
    /// <summary>
    /// The built-in agents with their default and effective tools for <paramref name="userId"/>.
    /// </summary>
    Task<IReadOnlyList<BuiltInAgentResponse>> GetAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Replaces the tools of built-in <paramref name="agentName"/> for <paramref name="userId"/>.
    /// <see cref="ResultStatus.NotFound"/> for an unknown or control agent.
    /// </summary>
    Task<Result<BuiltInAgentResponse>> SetToolsAsync(string agentName, Guid userId, IReadOnlyList<string>? tools, CancellationToken ct = default);

    /// <summary>
    /// Restores the code-defined tools of <paramref name="agentName"/>. <see cref="ResultStatus.NotFound"/> for an unknown
    /// or control agent.
    /// </summary>
    Task<Result<BuiltInAgentResponse>> ResetToolsAsync(string agentName, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// The tools <paramref name="agent"/> gets for <paramref name="userId"/>: the override when one exists, else the agent itself.
    /// </summary>
    Task<IAgent> ApplyOverrideAsync(IAgent agent, Guid? userId, CancellationToken ct = default);
}
