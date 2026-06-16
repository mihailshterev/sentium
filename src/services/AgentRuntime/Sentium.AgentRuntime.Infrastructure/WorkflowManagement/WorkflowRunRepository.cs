using Sentium.AgentRuntime.Core.Dtos;
using Sentium.AgentRuntime.Core.Entities;
using Sentium.AgentRuntime.Core.WorkflowManagement;
using Sentium.AgentRuntime.Infrastructure.Data;
using Sentium.AgentRuntime.Infrastructure.Projections;
using Microsoft.EntityFrameworkCore;

namespace Sentium.AgentRuntime.Infrastructure.WorkflowManagement;

public sealed class WorkflowRunRepository(AgentRuntimeDbContext context) : IWorkflowRunRepository
{
    public async Task AddAsync(WorkflowRun run, CancellationToken ct = default)
    {
        context.WorkflowRuns.Add(run);
        await context.SaveChangesAsync(ct);
    }

    public async Task<(IReadOnlyList<WorkflowRunResponse> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var total = await context.WorkflowRuns.CountAsync(ct);
        var items = await context.WorkflowRuns
            .AsNoTracking()
            .OrderByDescending(r => r.StartedAt)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsAsyncEnumerable()
            .Select(WorkflowRunProjections.ToResponse)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<WorkflowRunResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.WorkflowRuns
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(r => WorkflowRunProjections.ToResponse(r))
            .FirstOrDefaultAsync(ct);
    }
}
