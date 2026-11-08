using Sentium.Core.Common;

namespace Sentium.Praxis.Entities;

/// <summary>
/// A user's replacement for a built-in agent's code-defined tool list. No row means the built-in default applies.
/// </summary>
public sealed class AgentToolOverride : IUserOwned
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>The built-in agent's name (see <c>AgentRole</c>).</summary>
    public string AgentName { get; set; } = null!;

    /// <summary>Names of the tools the agent may use; <see langword="null"/> = every tool.</summary>
    public List<string>? Tools { get; set; }

    public DateTime UpdatedAt { get; set; }
}
