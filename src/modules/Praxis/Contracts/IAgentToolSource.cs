using Microsoft.Extensions.AI;

namespace Sentium.Praxis.Contracts;

/// <summary>
/// A pluggable provider of agent tools that are not compiled into Praxis and may change at runtime (for example MCP
/// servers). Praxis lists a source's tools in the tool catalogue and adds its functions to an agent only when the agent's
/// tool list names them explicitly, so registering a source never silently widens what existing agents can do.
/// Register implementations as <see cref="IAgentToolSource"/> services.
/// </summary>
public interface IAgentToolSource
{
    /// <summary>
    /// The tools that are currently usable, described for the tool picker.
    /// </summary>
    IReadOnlyList<ToolDescriptor> Descriptors { get; }

    /// <summary>
    /// Whether <paramref name="name"/> is a tool this source knows - including currently unusable ones, so agents that
    /// still list a disabled or changed tool keep validating.
    /// </summary>
    bool Contains(string name);

    /// <summary>
    /// Whether <paramref name="name"/> is shaped like one of this source's tool names (it need not exist).
    /// </summary>
    bool Owns(string name);

    /// <summary>
    /// The usable functions among <paramref name="names"/>, with approval-gated tools wrapped in
    /// <see cref="ApprovalRequiredAIFunction"/>. Empty when the source is disabled or owns none of the names.
    /// </summary>
    ValueTask<IReadOnlyList<AIFunction>> GetFunctionsAsync(IReadOnlySet<string> names, CancellationToken ct);
}
