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

    public async Task<IReadOnlyList<ExecutionLogEntry>> GetRecentAsync(int count = 100, CancellationToken ct = default)
        => await dbContext.ExecutionLogs
            .AsNoTracking()
            .OrderByDescending(e => e.ExecutedAt)
            .Take(count)
            .ToListAsync(ct);
}
