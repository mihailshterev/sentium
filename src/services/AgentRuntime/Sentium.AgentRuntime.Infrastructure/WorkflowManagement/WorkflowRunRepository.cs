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

    public async Task<IReadOnlyList<WorkflowRunResponse>> GetRecentAsync(int count = 20, CancellationToken ct = default)
        => await context.WorkflowRuns
            .AsNoTracking()
            .OrderByDescending(r => r.StartedAt)
            .Take(count)
            .AsAsyncEnumerable()
            .Select(WorkflowRunProjections.ToResponse)
            .ToListAsync(ct);

    public async Task<WorkflowRunResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.WorkflowRuns
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(r => WorkflowRunProjections.ToResponse(r))
            .FirstOrDefaultAsync(ct);
    }
}
