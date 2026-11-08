using Sentium.Core.Results;
using Sentium.Infrastructure.Security;
using Sentium.Tempus.Contracts;
using Sentium.Tempus.Contracts.Models;

namespace Sentium.Tempus.Features.Agents;

/// <summary>
/// Singleton facade over <see cref="TimelineAgentAccessService"/> for other modules. Each call runs in its own DI
/// scope that is marked as a system scope BEFORE anything is resolved: agent runs (workflows, schedules, approval
/// resumes) have no HTTP user, so the DbContext's per-user filter would otherwise compare against an empty id and
/// hide everything. The service compensates by filtering on the explicit user id of every call.
/// </summary>
internal sealed class ScopedTimelineAgentAccess(IServiceScopeFactory scopeFactory) : ITimelineAgentAccess
{
    public Task<TimelineAgentOverview> GetOverviewAsync(Guid userId, int? year = null, int upcomingDays = 14, CancellationToken ct = default)
        => RunAsync(s => s.GetOverviewAsync(userId, year, upcomingDays, ct));

    public Task<IReadOnlyList<TimelineTaskInfo>> ListTasksAsync(Guid userId, TimelineTaskQuery query, CancellationToken ct = default)
        => RunAsync(s => s.ListTasksAsync(userId, query, ct));

    public Task<TimelineTaskDetail?> GetTaskAsync(Guid userId, Guid taskId, CancellationToken ct = default)
        => RunAsync(s => s.GetTaskAsync(userId, taskId, ct));

    public Task<Result<TimelineProposalReceipt>> ProposeTaskAsync(Guid userId, string agentName, TimelineTaskProposal proposal, CancellationToken ct = default)
        => RunAsync(s => s.ProposeTaskAsync(userId, agentName, proposal, ct));

    public Task<Result<TimelineProposalReceipt>> ProposeRescheduleAsync(Guid userId, string agentName, TimelineRescheduleProposal proposal, CancellationToken ct = default)
        => RunAsync(s => s.ProposeRescheduleAsync(userId, agentName, proposal, ct));

    public Task<Result<TimelineNoteReceipt>> AddNoteAsync(Guid userId, string agentName, TimelineNoteInput note, CancellationToken ct = default)
        => RunAsync(s => s.AddNoteAsync(userId, agentName, note, ct));

    private async Task<T> RunAsync<T>(Func<TimelineAgentAccessService, Task<T>> action)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();
        return await action(scope.ServiceProvider.GetRequiredService<TimelineAgentAccessService>());
    }
}
