using Sentium.AgentRuntime.Core.Dtos;
using Sentium.AgentRuntime.Core.WorkflowManagement;

namespace Sentium.AgentRuntime.Application.WorkflowManagement;

public sealed class WorkflowService(IWorkflowRepository repository) : IWorkflowService
{
    public Task<IReadOnlyList<WorkflowResponse>> GetWorkflowsAsync(CancellationToken ct = default)
        => repository.GetWorkflowsAsync(ct);

    public Task<WorkflowResponse> GetWorkflowAsync(Guid workflowId, CancellationToken ct = default)
        => repository.GetWorkflowAsync(workflowId, ct);

    public Task<WorkflowResponse> CreateWorkflowAsync(CreateWorkflowRequest request, CancellationToken ct = default)
        => repository.CreateWorkflowAsync(request, ct);

    public Task UpdateWorkflowAsync(Guid workflowId, UpdateWorkflowRequest request, CancellationToken ct = default)
        => repository.UpdateWorkflowAsync(workflowId, request, ct);

    public Task DeleteWorkflowAsync(Guid workflowId, CancellationToken ct = default)
        => repository.DeleteWorkflowAsync(workflowId, ct);
}
