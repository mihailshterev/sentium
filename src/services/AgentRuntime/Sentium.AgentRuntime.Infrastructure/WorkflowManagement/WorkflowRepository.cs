using Sentium.AgentRuntime.Core.Dtos;
using Sentium.AgentRuntime.Core.Entities;
using Sentium.AgentRuntime.Core.WorkflowManagement;
using Sentium.AgentRuntime.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Sentium.AgentRuntime.Infrastructure.WorkflowManagement;

public sealed class WorkflowRepository(AgentRuntimeDbContext context) : IWorkflowRepository
{
    public async Task<IReadOnlyList<WorkflowResponse>> GetWorkflowsAsync(CancellationToken ct = default)
    {
        return await context.Workflows
            .AsNoTracking()
            .Include(w => w.WorkflowAgents)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new WorkflowResponse(
                w.Id,
                w.UserId,
                w.Name,
                w.Description,
                w.CreatedAt,
                w.UpdatedAt,
                w.WorkflowAgents
                    .OrderBy(wa => wa.Order)
                    .Select(wa => new WorkflowAgentRef(wa.AgentId, wa.Order))
                    .ToList()))
            .ToListAsync(ct);
    }

    public async Task<WorkflowResponse?> GetWorkflowAsync(Guid workflowId, CancellationToken ct = default)
    {
        var workflow = await context.Workflows
            .AsNoTracking()
            .Include(w => w.WorkflowAgents)
            .FirstOrDefaultAsync(w => w.Id == workflowId, ct);

        if (workflow is null)
        {
            return null;
        }

        return new WorkflowResponse(
            workflow.Id,
            workflow.UserId,
            workflow.Name,
            workflow.Description,
            workflow.CreatedAt,
            workflow.UpdatedAt,
            workflow.WorkflowAgents
                .OrderBy(wa => wa.Order)
                .Select(wa => new WorkflowAgentRef(wa.AgentId, wa.Order))
                .ToList());
    }

    public async Task<WorkflowResponse> CreateWorkflowAsync(CreateWorkflowRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var workflow = new Workflow
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkflowAgents = request.Agents
                .Select(a => new WorkflowAgent { AgentId = a.AgentId, Order = a.Order })
                .ToList()
        };

        context.Workflows.Add(workflow);
        await context.SaveChangesAsync(ct);

        return new WorkflowResponse(
            workflow.Id,
            workflow.UserId,
            workflow.Name,
            workflow.Description,
            workflow.CreatedAt,
            workflow.UpdatedAt,
            workflow.WorkflowAgents
                .OrderBy(wa => wa.Order)
                .Select(wa => new WorkflowAgentRef(wa.AgentId, wa.Order))
                .ToList());
    }

    public async Task<bool> UpdateWorkflowAsync(Guid workflowId, UpdateWorkflowRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var workflow = await context.Workflows
            .Include(w => w.WorkflowAgents)
            .FirstOrDefaultAsync(w => w.Id == workflowId, ct);

        if (workflow is null)
        {
            return false;
        }

        workflow.Name = request.Name;
        workflow.Description = request.Description;
        workflow.UpdatedAt = DateTime.UtcNow;

        workflow.WorkflowAgents.Clear();
        foreach (var a in request.Agents)
        {
            workflow.WorkflowAgents.Add(new WorkflowAgent { WorkflowId = workflowId, AgentId = a.AgentId, Order = a.Order });
        }

        await context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteWorkflowAsync(Guid workflowId, CancellationToken ct = default)
    {
        var affected = await context.Workflows
            .Where(w => w.Id == workflowId)
            .ExecuteDeleteAsync(ct);

        return affected > 0;
    }
}
