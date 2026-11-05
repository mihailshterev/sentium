using Sentium.Praxis.Dtos;
using Sentium.Praxis.Entities;
using Sentium.Praxis.Data;
using Sentium.Praxis.Features.Projections;
using Microsoft.EntityFrameworkCore;

namespace Sentium.Praxis.Features.WorkflowManagement;

public sealed class WorkflowRunRepository(PraxisDbContext context) : IWorkflowRunRepository
{
    public async Task AddAsync(WorkflowRun run, CancellationToken ct = default)
    {
        context.WorkflowRuns.Add(run);
        await context.SaveChangesAsync(ct);
    }

    public async Task<(IReadOnlyList<WorkflowRunSummaryResponse> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var total = await context.WorkflowRuns.CountAsync(ct);
        var items = await context.WorkflowRuns
            .AsNoTracking()
            .OrderByDescending(r => r.StartedAt)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(WorkflowRunProjections.ToSummary)
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
