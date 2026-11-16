using Sentium.Sentinel.Features.Audit;

namespace Sentium.Tests.Unit.Modules.Sentinel;

/// <summary>
/// In-memory <see cref="IAuditLog"/> for tests that need the audit trail without a database.
/// </summary>
internal sealed class InMemoryAuditLog : IAuditLog
{
    private readonly List<AuditRecord> _records = [];
    private readonly Lock _lock = new();

    public IReadOnlyList<AuditRecord> Records
    {
        get
        {
            lock (_lock)
            {
                return [.. _records];
            }
        }
    }

    public ValueTask RecordAsync(AuditRecord record, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _records.Insert(0, record);
        }

        return ValueTask.CompletedTask;
    }

    public Task<(IReadOnlyList<AuditRecord> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        return Task.FromResult(Page(Records, page, pageSize));
    }

    public Task<(IReadOnlyList<AuditRecord> Items, int TotalCount)> GetByAgentPagedAsync(string agentId, int page, int pageSize, CancellationToken ct = default)
    {
        return Task.FromResult(Page([.. Records.Where(r => r.AgentId == agentId)], page, pageSize));
    }

    public Task<AuditStatsDto> GetStatsAsync(int window, CancellationToken ct = default)
    {
        var outcomes = Records.Take(window).Select(r => new AuditOutcome(r.Allowed, r.Effect, r.Risk, r.AlignmentVerdict)).ToList();
        return Task.FromResult(AuditStatsCalculator.Calculate(outcomes));
    }

    public Task<int> PurgeOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_records.RemoveAll(r => r.Timestamp < cutoff));
        }
    }

    private static (IReadOnlyList<AuditRecord> Items, int TotalCount) Page(IReadOnlyList<AuditRecord> records, int page, int pageSize)
    {
        return ([.. records.Skip((page - 1) * pageSize).Take(pageSize)], records.Count);
    }
}
