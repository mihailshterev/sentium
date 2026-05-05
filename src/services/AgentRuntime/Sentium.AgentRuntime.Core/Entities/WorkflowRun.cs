namespace Sentium.AgentRuntime.Core.Entities;

public sealed class WorkflowRun
{
    public Guid Id { get; set; }
    public string TriggerType { get; set; } = null!;
    public string TriggerPayload { get; set; } = null!;
    public string Explanation { get; set; } = null!;
    public string Risk { get; set; } = null!;
    public string Recommendation { get; set; } = null!;
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public string? LogJson { get; set; }
}
