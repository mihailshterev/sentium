using AgentRuntime.Core.Dtos;
using AgentRuntime.Core.Entities;

namespace AgentRuntime.Core.WorkflowManagement;

public interface IWorkflowRunRepository
{
    Task AddAsync(WorkflowRun run, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowRunResponse>> GetRecentAsync(int count = 20, CancellationToken ct = default);
}
