namespace Sentium.AgentRuntime.Core.Entities;

public sealed class Workspace
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<ProjectFile> Files { get; set; } = [];
}
