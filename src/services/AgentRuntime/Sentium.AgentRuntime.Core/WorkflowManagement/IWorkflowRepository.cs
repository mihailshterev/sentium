using Sentium.AgentRuntime.Core.Dtos;

namespace Sentium.AgentRuntime.Core.WorkflowManagement;

public interface IWorkflowRepository
{
    Task<IReadOnlyList<WorkflowResponse>> GetWorkflowsAsync(CancellationToken ct = default);
    Task<WorkflowResponse?> GetWorkflowAsync(Guid workflowId, CancellationToken ct = default);
    Task<WorkflowResponse> CreateWorkflowAsync(CreateWorkflowRequest request, CancellationToken ct = default);
    Task<bool> UpdateWorkflowAsync(Guid workflowId, UpdateWorkflowRequest request, CancellationToken ct = default);
    Task<bool> DeleteWorkflowAsync(Guid workflowId, CancellationToken ct = default);
}
