namespace Sentium.Sentinel.Infrastructure.Data;

public sealed class AuditLogEntity
{
    public Guid Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string AgentId { get; set; } = "";
    public string SkillName { get; set; } = "";
    public string ResourceType { get; set; } = "";
    public string ResourceId { get; set; } = "";
    public string Action { get; set; } = "";
    public string UserPromptHash { get; set; } = "";
    public string CorrelationId { get; set; } = "";
    public string MetadataJson { get; set; } = "{}";
    public bool Allowed { get; set; }
    public string Effect { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Risk { get; set; } = "";
    public string TriggeredPoliciesJson { get; set; } = "[]";
    public long EvaluationDurationMs { get; set; }
    public string? AlignmentVerdict { get; set; }
}
