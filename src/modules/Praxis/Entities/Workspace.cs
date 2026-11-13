using Sentium.Core.Common;

namespace Sentium.Praxis.Entities;

/// <summary>
/// A user's grouping of <see cref="ProjectFile"/>s that the agent can access, ingested for RAG.
/// </summary>
public sealed class Workspace : IUserOwned
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>
    /// Unique per user.
    /// </summary>
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<ProjectFile> Files { get; set; } = [];
}
