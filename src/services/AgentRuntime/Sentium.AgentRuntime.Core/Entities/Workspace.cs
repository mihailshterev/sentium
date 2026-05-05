namespace Sentium.AgentRuntime.Core.Entities;

/// <summary>
/// Represents a user workspace that serves as a container for organizing project files and context.
/// </summary>
/// <remarks>
/// Workspaces provide a logical grouping mechanism for files that the agent can access and process.
/// Each workspace can contain multiple files (see <see cref="ProjectFile"/>) and is associated with 
/// file uploads, RAG (Retrieval-Augmented Generation) ingestion, and agent context management.
/// </remarks>
public sealed class Workspace
{
    /// <summary>
    /// Gets or sets the unique identifier for this workspace.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the workspace.
    /// </summary>
    /// <remarks>
    /// Workspace names must be unique within the system.
    /// </remarks>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Gets or sets an optional description providing context about the workspace's purpose.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this workspace was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the most recent update to this workspace.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the collection of files within this workspace.
    /// </summary>
    /// <remarks>
    /// This navigation property provides access to all <see cref="ProjectFile"/> entities associated with this workspace.
    /// </remarks>
    public ICollection<ProjectFile> Files { get; set; } = [];
}
