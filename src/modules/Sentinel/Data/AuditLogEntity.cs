using Sentium.Core.Common;

namespace Sentium.Sentinel.Data;

/// <summary>
/// Not change-logged by the audit interceptor: the row is the audit record.
/// </summary>
internal sealed class AuditLogEntity : INonAudited
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
    public Guid? UserId { get; set; }
    public string MetadataJson { get; set; } = "{}";
    public bool Allowed { get; set; }
    public string Effect { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Risk { get; set; } = "";
    public string TriggeredPoliciesJson { get; set; } = "[]";
    public long EvaluationDurationMs { get; set; }
    public string? AlignmentVerdict { get; set; }
}
