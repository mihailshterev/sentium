using Sentium.Praxis.Common;
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

    public async Task<(IReadOnlyList<WorkflowRunSummaryResponse> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search = null, CancellationToken ct = default)
    {
        IQueryable<WorkflowRun> runs = context.WorkflowRuns.AsNoTracking();
        if (TextSearch.Normalize(search) is { } term)
        {
            var pattern = TextSearch.ToContainsPattern(term);
            runs = runs.Where(r =>
                (r.Title != null && EF.Functions.ILike(r.Title, pattern, TextSearch.EscapeCharacter))
                || (r.Workflow != null && EF.Functions.ILike(r.Workflow.Name, pattern, TextSearch.EscapeCharacter))
                || (r.Title == null && EF.Functions.ILike(r.TriggerPayload, pattern, TextSearch.EscapeCharacter)));
        }

        var total = await runs.CountAsync(ct);
        var items = await runs
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

    public async Task<bool> RenameAsync(Guid id, string title, CancellationToken ct = default)
    {
        var affected = await context.WorkflowRuns
            .Where(r => r.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Title, title), ct);

        return affected > 0;
    }
}
