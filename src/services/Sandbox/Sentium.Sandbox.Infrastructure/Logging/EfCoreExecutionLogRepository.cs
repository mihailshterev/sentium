using Microsoft.EntityFrameworkCore;
using Sentium.Sandbox.Core.Interfaces;
using Sentium.Sandbox.Core.Models;
using Sentium.Sandbox.Infrastructure.Data;

namespace Sentium.Sandbox.Infrastructure.Logging;

public sealed class EfCoreExecutionLogRepository(SandboxDbContext dbContext) : IExecutionLogRepository
{
    public async Task AddAsync(ExecutionLogEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        dbContext.ExecutionLogs.Add(entry);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<(IReadOnlyList<ExecutionLogEntry> Items, int TotalCount)> GetPagedAsync(
        ExecutionLogQuery query,
        CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var filtered = ApplyFilters(dbContext.ExecutionLogs.AsNoTracking(), query);

        var total = await filtered.CountAsync(ct);
        var items = await filtered
            .OrderByDescending(e => e.ExecutedAt)
            .ThenByDescending(e => e.JobId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<ExecutionLogEntry?> GetByIdAsync(Guid jobId, CancellationToken ct = default)
        => await dbContext.ExecutionLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.JobId == jobId, ct);

    public async Task<ExecutionLogStats> GetStatsAsync(CancellationToken ct = default)
    {
        var logs = dbContext.ExecutionLogs.AsNoTracking();

        var total = await logs.CountAsync(ct);
        var denied = await logs.CountAsync(e => e.PolicyDenied, ct);
        var succeeded = await logs.CountAsync(e => e.Succeeded && !e.PolicyDenied, ct);
        var failed = total - denied - succeeded;

        return new ExecutionLogStats(total, succeeded, failed, denied);
    }

    private static IQueryable<ExecutionLogEntry> ApplyFilters(IQueryable<ExecutionLogEntry> source, ExecutionLogQuery query)
    {
        if (query.Language is { } language)
        {
            source = source.Where(e => e.Language == language);
        }

        source = query.Status switch
        {
            ExecutionStatusFilter.Succeeded => source.Where(e => e.Succeeded && !e.PolicyDenied),
            ExecutionStatusFilter.Failed => source.Where(e => !e.Succeeded && !e.PolicyDenied),
            ExecutionStatusFilter.Denied => source.Where(e => e.PolicyDenied),
            _ => source
        };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            source = source.Where(e => e.AgentId.Contains(term) || e.JobId.ToString().Contains(term));
        }

        return source;
    }
}
