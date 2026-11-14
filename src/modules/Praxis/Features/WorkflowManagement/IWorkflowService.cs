using Sentium.Core.Results;
using Sentium.Praxis.Features.Workflows;

namespace Sentium.Praxis.Features.WorkflowManagement;

/// <summary>
/// Application-layer operations for agent workflows.
/// </summary>
internal interface IWorkflowService
{
    Task<IReadOnlyList<WorkflowResponse>> GetWorkflowsAsync(CancellationToken ct = default);
    Task<PagedResponse<WorkflowResponse>> GetWorkflowsPagedAsync(int page, int pageSize, CancellationToken ct = default);
    Task<WorkflowResponse?> GetWorkflowAsync(Guid workflowId, CancellationToken ct = default);
    Task<WorkflowResponse> CreateWorkflowAsync(CreateWorkflowRequest request, CancellationToken ct = default);
    Task<bool> UpdateWorkflowAsync(Guid workflowId, UpdateWorkflowRequest request, CancellationToken ct = default);
    Task<bool> DeleteWorkflowAsync(Guid workflowId, CancellationToken ct = default);
}
