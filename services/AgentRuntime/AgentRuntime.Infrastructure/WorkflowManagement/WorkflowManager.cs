using AgentRuntime.Core.Dtos;
using AgentRuntime.Core.Entities;
using AgentRuntime.Core.WorkflowManagement;
using AgentRuntime.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgentRuntime.Infrastructure.WorkflowManagement;

public sealed class WorkflowManager(AgentRuntimeDbContext context) : IWorkflowManager
{
    public async Task<IReadOnlyList<WorkflowResponse>> GetWorkflowsAsync(CancellationToken ct = default)
    {
        return await context.Workflows
            .AsNoTracking()
            .Include(w => w.WorkflowAgents)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new WorkflowResponse(
                w.Id,
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

    public async Task<WorkflowResponse> GetWorkflowAsync(Guid workflowId, CancellationToken ct = default)
    {
        var workflow = await context.Workflows
            .AsNoTracking()
            .Include(w => w.WorkflowAgents)
            .FirstOrDefaultAsync(w => w.Id == workflowId, ct)
            ?? throw new KeyNotFoundException($"Workflow {workflowId} not found.");

        return new WorkflowResponse(
            workflow.Id,
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
            workflow.Name,
            workflow.Description,
            workflow.CreatedAt,
            workflow.UpdatedAt,
            workflow.WorkflowAgents
                .OrderBy(wa => wa.Order)
                .Select(wa => new WorkflowAgentRef(wa.AgentId, wa.Order))
                .ToList());
    }

    public async Task UpdateWorkflowAsync(Guid workflowId, UpdateWorkflowRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var workflow = await context.Workflows
            .Include(w => w.WorkflowAgents)
            .FirstOrDefaultAsync(w => w.Id == workflowId, ct)
            ?? throw new KeyNotFoundException($"Workflow {workflowId} not found.");

        workflow.Name = request.Name;
        workflow.Description = request.Description;
        workflow.UpdatedAt = DateTime.UtcNow;

        workflow.WorkflowAgents.Clear();
        foreach (var a in request.Agents)
        {
            workflow.WorkflowAgents.Add(new WorkflowAgent { WorkflowId = workflowId, AgentId = a.AgentId, Order = a.Order });
        }

        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteWorkflowAsync(Guid workflowId, CancellationToken ct = default)
    {
        var affected = await context.Workflows
            .Where(w => w.Id == workflowId)
            .ExecuteDeleteAsync(ct);

        if (affected == 0)
        {
            throw new KeyNotFoundException($"Workflow {workflowId} not found.");
        }
    }
}
