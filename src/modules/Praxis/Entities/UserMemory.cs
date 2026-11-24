using Sentium.Core.Common;

namespace Sentium.Praxis.Entities;

/// <summary>
/// A personal fact or preference the user asked an agent to remember. Private to the user, Sovereigns included. Its
/// one vector in Codex's <c>user_memories</c> collection shares its id and has the source <c>memory:{Id}</c>.
/// </summary>
internal sealed class UserMemory : IUserOwned
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Content { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
