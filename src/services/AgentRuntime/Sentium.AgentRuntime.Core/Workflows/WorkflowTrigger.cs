namespace Sentium.AgentRuntime.Core.Workflows;

public sealed class WorkflowTrigger
{
    public string TriggerType { get; init; } = "";
    public string Payload { get; init; } = "";
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public Guid? UserId { get; init; }

    public string StreamId
    {
        get => string.IsNullOrEmpty(field) ? TriggerType : field;
        init;
    }
}
