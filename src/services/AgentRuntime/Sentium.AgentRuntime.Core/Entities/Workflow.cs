namespace Sentium.AgentRuntime.Core.Entities;

public sealed class Workflow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<WorkflowAgent> WorkflowAgents { get; set; } = [];
}
