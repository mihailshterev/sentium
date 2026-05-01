using AgentRuntime.Core.Dtos;
using AgentRuntime.Core.Entities;
using AgentRuntime.Core.WorkflowManagement;
using AgentRuntime.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgentRuntime.Infrastructure.WorkflowManagement;

public sealed class WorkflowRunRepository(AgentRuntimeDbContext context) : IWorkflowRunRepository
{
    public async Task AddAsync(WorkflowRun run, CancellationToken ct = default)
    {
        context.WorkflowRuns.Add(run);
        await context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<WorkflowRunResponse>> GetRecentAsync(int count = 20, CancellationToken ct = default)
    {
        return await context.WorkflowRuns
            .AsNoTracking()
            .OrderByDescending(r => r.StartedAt)
            .Take(count)
            .Select(r => new WorkflowRunResponse(
                r.Id,
                r.TriggerType,
                r.TriggerPayload,
                r.Explanation,
                r.Risk,
                r.Recommendation,
                r.StartedAt,
                r.CompletedAt))
            .ToListAsync(ct);
    }
}
