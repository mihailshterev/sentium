using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sentium.Sentinel.Core.Audit;
using Sentium.Sentinel.Core.Policies;
using Sentium.Sentinel.Infrastructure.Data;

namespace Sentium.Sentinel.Infrastructure.Audit;

public sealed class EfCoreAuditLog(SentinelDbContext dbContext) : IAuditLog
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async ValueTask RecordAsync(AuditRecord record, CancellationToken ct = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(record);
            dbContext.AuditLogs.Add(MapToEntity(record));
            await dbContext.SaveChangesAsync(ct);
        }
        catch
        {
            // Audit failures must never break the caller's flow.
        }
    }

    public async Task<IReadOnlyList<AuditRecord>> GetRecentAsync(int count = 100, CancellationToken ct = default)
    {
        var entities = await dbContext.AuditLogs
            .AsNoTracking()
            .OrderByDescending(e => e.Timestamp)
            .Take(Math.Max(1, Math.Min(count, 500)))
            .ToListAsync(ct);

        return entities.Select(MapToRecord).ToList();
    }

    public async Task<IReadOnlyList<AuditRecord>> GetByAgentAsync(string agentId, int count = 50, CancellationToken ct = default)
    {
        var entities = await dbContext.AuditLogs
            .AsNoTracking()
            .Where(e => e.AgentId == agentId)
            .OrderByDescending(e => e.Timestamp)
            .Take(Math.Max(1, Math.Min(count, 200)))
            .ToListAsync(ct);

        return entities.Select(MapToRecord).ToList();
    }

    private static AuditLogEntity MapToEntity(AuditRecord record) => new()
    {
        Id = record.Id,
        Timestamp = record.Timestamp,
        AgentId = record.AgentId,
        SkillName = record.SkillName,
        ResourceType = record.ResourceType.ToString(),
        ResourceId = record.ResourceId,
        Action = record.Action,
        UserPromptHash = record.UserPromptHash,
        CorrelationId = record.CorrelationId,
        MetadataJson = JsonSerializer.Serialize(record.Metadata, JsonOpts),
        Allowed = record.Allowed,
        Effect = record.Effect.ToString(),
        Reason = record.Reason,
        Risk = record.Risk.ToString(),
        TriggeredPoliciesJson = JsonSerializer.Serialize(record.TriggeredPolicies, JsonOpts),
        EvaluationDurationMs = record.EvaluationDurationMs,
        AlignmentVerdict = record.AlignmentVerdict,
    };

    private static AuditRecord MapToRecord(AuditLogEntity e) => new()
    {
        Id = e.Id,
        Timestamp = e.Timestamp,
        AgentId = e.AgentId,
        SkillName = e.SkillName,
        ResourceType = Enum.Parse<ResourceType>(e.ResourceType),
        ResourceId = e.ResourceId,
        Action = e.Action,
        UserPromptHash = e.UserPromptHash,
        CorrelationId = e.CorrelationId,
        Metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(e.MetadataJson, JsonOpts) ?? [],
        Allowed = e.Allowed,
        Effect = Enum.Parse<PolicyEffect>(e.Effect),
        Reason = e.Reason,
        Risk = Enum.Parse<PolicyRiskLevel>(e.Risk),
        TriggeredPolicies = JsonSerializer.Deserialize<List<string>>(e.TriggeredPoliciesJson, JsonOpts) ?? [],
        EvaluationDurationMs = e.EvaluationDurationMs,
        AlignmentVerdict = e.AlignmentVerdict,
    };
}
