using Sentium.AgentRuntime.Core.Dtos;
using Sentium.AgentRuntime.Core.Entities;

namespace Sentium.AgentRuntime.Core.WorkflowManagement;

public interface IWorkflowRunRepository
{
    Task AddAsync(WorkflowRun run, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowRunResponse>> GetRecentAsync(int count = 20, CancellationToken ct = default);
}
