using AgentRuntime.Core.Dtos;

namespace AgentRuntime.Core.WorkflowManagement;

public interface IWorkflowService
{
    Task<IReadOnlyList<WorkflowResponse>> GetWorkflowsAsync(CancellationToken ct = default);
    Task<WorkflowResponse> GetWorkflowAsync(Guid workflowId, CancellationToken ct = default);
    Task<WorkflowResponse> CreateWorkflowAsync(CreateWorkflowRequest request, CancellationToken ct = default);
    Task UpdateWorkflowAsync(Guid workflowId, UpdateWorkflowRequest request, CancellationToken ct = default);
    Task DeleteWorkflowAsync(Guid workflowId, CancellationToken ct = default);
}
