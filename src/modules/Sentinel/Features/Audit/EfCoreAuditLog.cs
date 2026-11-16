using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sentium.Sentinel.Features.Policies;
using Sentium.Sentinel.Data;

namespace Sentium.Sentinel.Features.Audit;

internal sealed class EfCoreAuditLog(SentinelDbContext dbContext, ILogger<EfCoreAuditLog> logger) : IAuditLog
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async ValueTask RecordAsync(AuditRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        try
        {
            dbContext.AuditLogs.Add(MapToEntity(record));
            await dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to write audit record {AuditId} ({Effect} {Skill} for agent '{AgentId}'). The decision stands but is missing from the audit trail.",
                record.Id, record.Effect, record.SkillName, record.AgentId);
        }
    }

    public async Task<(IReadOnlyList<AuditRecord> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var total = await dbContext.AuditLogs.CountAsync(ct);
        var entities = await dbContext.AuditLogs
            .AsNoTracking()
            .OrderByDescending(e => e.Timestamp)
            .ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (entities.Select(MapToRecord).ToList(), total);
    }

    public async Task<(IReadOnlyList<AuditRecord> Items, int TotalCount)> GetByAgentPagedAsync(string agentId, int page, int pageSize, CancellationToken ct = default)
    {
        var filtered = dbContext.AuditLogs.AsNoTracking().Where(e => e.AgentId == agentId);

        var total = await filtered.CountAsync(ct);
        var entities = await filtered
            .OrderByDescending(e => e.Timestamp)
            .ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (entities.Select(MapToRecord).ToList(), total);
    }

    public async Task<AuditStatsDto> GetStatsAsync(int window, CancellationToken ct = default)
    {
        var rows = await dbContext.AuditLogs
            .AsNoTracking()
            .OrderByDescending(e => e.Timestamp)
            .Take(Math.Max(1, window))
            .Select(e => new { e.Allowed, e.Effect, e.Risk, e.AlignmentVerdict })
            .ToListAsync(ct);

        return AuditStatsCalculator.Calculate(
            [.. rows.Select(r => new AuditOutcome(r.Allowed, Enum.Parse<PolicyEffect>(r.Effect), Enum.Parse<PolicyRiskLevel>(r.Risk), r.AlignmentVerdict))]);
    }

    public Task<int> PurgeOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default)
    {
        return dbContext.AuditLogs.Where(e => e.Timestamp < cutoff).ExecuteDeleteAsync(ct);
    }

    private static AuditLogEntity MapToEntity(AuditRecord record)
    {
        return new()
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
            UserId = record.UserId,
            MetadataJson = JsonSerializer.Serialize(record.Metadata, JsonOpts),
            Allowed = record.Allowed,
            Effect = record.Effect.ToString(),
            Reason = record.Reason,
            Risk = record.Risk.ToString(),
            TriggeredPoliciesJson = JsonSerializer.Serialize(record.TriggeredPolicies, JsonOpts),
            EvaluationDurationMs = record.EvaluationDurationMs,
            AlignmentVerdict = record.AlignmentVerdict,
        };
    }

    private static AuditRecord MapToRecord(AuditLogEntity e)
    {
        return new()
        {
            Id = e.Id,
            Timestamp = e.Timestamp,
            AgentId = e.AgentId,
            SkillName = e.SkillName,
            ResourceType = Enum.TryParse<ResourceType>(e.ResourceType, out var rt) ? rt : ResourceType.File,
            ResourceId = e.ResourceId,
            Action = e.Action,
            UserPromptHash = e.UserPromptHash,
            CorrelationId = e.CorrelationId,
            UserId = e.UserId,
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
}
