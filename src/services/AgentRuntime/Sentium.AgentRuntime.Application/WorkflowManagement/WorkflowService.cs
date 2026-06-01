using Sentium.AgentRuntime.Core.Dtos;
using Sentium.AgentRuntime.Core.WorkflowManagement;
using Sentium.Infrastructure.Caching;

namespace Sentium.AgentRuntime.Application.WorkflowManagement;

public sealed class WorkflowService(
    IWorkflowRepository repository,
    IScopedCache cache) : IWorkflowService
{
    private const string CacheTag = "workflows";

    public async Task<IReadOnlyList<WorkflowResponse>> GetWorkflowsAsync(CancellationToken ct = default)
        => await cache.GetOrCreateAsync(
            $"{CacheTag}:all",
            async token => await repository.GetWorkflowsAsync(token),
            CacheTag,
            ct);

    public async Task<WorkflowResponse> GetWorkflowAsync(Guid workflowId, CancellationToken ct = default)
        => await cache.GetOrCreateAsync(
            $"{CacheTag}:{workflowId}",
            async token => await repository.GetWorkflowAsync(workflowId, token),
            CacheTag,
            ct);

    public async Task<WorkflowResponse> CreateWorkflowAsync(CreateWorkflowRequest request, CancellationToken ct = default)
    {
        var result = await repository.CreateWorkflowAsync(request, ct);
        await cache.InvalidateTagAsync(CacheTag, ct);
        return result;
    }

    public async Task UpdateWorkflowAsync(Guid workflowId, UpdateWorkflowRequest request, CancellationToken ct = default)
    {
        await repository.UpdateWorkflowAsync(workflowId, request, ct);
        await cache.InvalidateTagAsync(CacheTag, ct);
    }

    public async Task DeleteWorkflowAsync(Guid workflowId, CancellationToken ct = default)
    {
        await repository.DeleteWorkflowAsync(workflowId, ct);
        await cache.InvalidateTagAsync(CacheTag, ct);
    }
}
