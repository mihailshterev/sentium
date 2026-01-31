namespace AgentRuntime.Core.Workflows;

public sealed class WorkflowTrigger
{
    public string TriggerType { get; init; } = "";
    public string Payload { get; init; } = "";
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
