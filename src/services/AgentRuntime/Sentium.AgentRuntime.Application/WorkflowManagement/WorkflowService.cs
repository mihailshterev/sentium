using Sentium.AgentRuntime.Core.Dtos;
using Sentium.AgentRuntime.Core.WorkflowManagement;

namespace Sentium.AgentRuntime.Application.WorkflowManagement;

public sealed class WorkflowService(IWorkflowManager manager) : IWorkflowService
{
    public Task<IReadOnlyList<WorkflowResponse>> GetWorkflowsAsync(CancellationToken ct = default)
        => manager.GetWorkflowsAsync(ct);

    public Task<WorkflowResponse> GetWorkflowAsync(Guid workflowId, CancellationToken ct = default)
        => manager.GetWorkflowAsync(workflowId, ct);

    public Task<WorkflowResponse> CreateWorkflowAsync(CreateWorkflowRequest request, CancellationToken ct = default)
        => manager.CreateWorkflowAsync(request, ct);

    public Task UpdateWorkflowAsync(Guid workflowId, UpdateWorkflowRequest request, CancellationToken ct = default)
        => manager.UpdateWorkflowAsync(workflowId, request, ct);

    public Task DeleteWorkflowAsync(Guid workflowId, CancellationToken ct = default)
        => manager.DeleteWorkflowAsync(workflowId, ct);
}
