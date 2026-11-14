namespace Sentium.Praxis.Entities;

internal sealed class WorkflowAgent
{
    public Guid WorkflowId { get; set; }
    public Guid AgentId { get; set; }
    public int Order { get; set; }

    public Workflow Workflow { get; set; } = null!;
    public Agent Agent { get; set; } = null!;
}
