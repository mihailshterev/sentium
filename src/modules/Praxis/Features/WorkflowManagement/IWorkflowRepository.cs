using Sentium.Praxis.Features.Workflows;

namespace Sentium.Praxis.Features.WorkflowManagement;

internal interface IWorkflowRepository
{
    Task<IReadOnlyList<WorkflowResponse>> GetWorkflowsAsync(CancellationToken ct = default);
    Task<(IReadOnlyList<WorkflowResponse> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, CancellationToken ct = default);
    Task<WorkflowResponse?> GetWorkflowAsync(Guid workflowId, CancellationToken ct = default);
    Task<WorkflowResponse> CreateWorkflowAsync(CreateWorkflowRequest request, CancellationToken ct = default);
    Task<bool> UpdateWorkflowAsync(Guid workflowId, UpdateWorkflowRequest request, CancellationToken ct = default);
    Task<bool> DeleteWorkflowAsync(Guid workflowId, CancellationToken ct = default);

    /// <summary>
    /// The owner of a workflow the caller can see, or <see langword="null"/>.
    /// </summary>
    Task<Guid?> GetOwnerAsync(Guid workflowId, CancellationToken ct = default);
}
