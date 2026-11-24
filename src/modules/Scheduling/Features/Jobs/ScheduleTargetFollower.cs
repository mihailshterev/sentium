using Quartz;
using Quartz.Impl.Matchers;
using Sentium.Praxis.Contracts;

namespace Sentium.Scheduling.Features.Jobs;

/// <summary>
/// Keeps agent and workflow Schedules pointing at something that exists: a renamed agent's Schedules follow the new
/// name, and a deleted agent's or workflow's Schedules are deleted with it instead of failing on every fire. Only the
/// owner's Schedules (the Quartz group) can name their agents and workflows.
/// </summary>
internal sealed class ScheduleTargetFollower(ISchedulerFactory schedulerFactory, ILogger<ScheduleTargetFollower> logger) : IRunTargetObserver
{
    public async Task OnAgentRenamedAsync(Guid ownerId, string oldName, string newName, CancellationToken ct)
    {
        var scheduler = await schedulerFactory.GetScheduler(ct);

        foreach (var (detail, definition) in await SchedulesOfAsync(scheduler, ownerId, ct))
        {
            if (!TargetsAgent(definition, oldName))
            {
                continue;
            }

            var updated = detail.GetJobBuilder().Build();
            (definition with { AgentName = newName, TargetName = newName }).WriteTo(updated.JobDataMap);
            await scheduler.AddJob(updated, replace: true, storeNonDurableWhileAwaitingScheduling: true, ct);

            logger.LogInformation("Schedule {JobKey} now runs agent {AgentName}, renamed from {OldName}.", detail.Key, newName, oldName);
        }
    }

    public Task OnAgentDeletedAsync(Guid ownerId, string agentName, CancellationToken ct) =>
        DeleteWhereAsync(ownerId, definition => TargetsAgent(definition, agentName), $"agent {agentName}", ct);

    public Task OnWorkflowDeletedAsync(Guid ownerId, Guid workflowId, CancellationToken ct) =>
        DeleteWhereAsync(ownerId, definition => definition.Kind == ScheduleKind.Workflow && definition.WorkflowId == workflowId, $"workflow {workflowId}", ct);

    private static bool TargetsAgent(ScheduleDefinition definition, string agentName) =>
        definition.Kind == ScheduleKind.Agent && string.Equals(definition.AgentName, agentName, StringComparison.OrdinalIgnoreCase);

    private static async Task<List<(IJobDetail Detail, ScheduleDefinition Definition)>> SchedulesOfAsync(IScheduler scheduler, Guid ownerId, CancellationToken ct)
    {
        var schedules = new List<(IJobDetail, ScheduleDefinition)>();
        foreach (var key in await scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(SchedulerDefaults.GroupFor(ownerId)), ct))
        {
            if (await scheduler.GetJobDetail(key, ct) is { } detail && ScheduleDefinition.Read(detail.JobDataMap) is { } definition)
            {
                schedules.Add((detail, definition));
            }
        }

        return schedules;
    }

    private async Task DeleteWhereAsync(Guid ownerId, Func<ScheduleDefinition, bool> targets, string target, CancellationToken ct)
    {
        var scheduler = await schedulerFactory.GetScheduler(ct);

        foreach (var (detail, definition) in await SchedulesOfAsync(scheduler, ownerId, ct))
        {
            if (targets(definition) && await scheduler.DeleteJob(detail.Key, ct))
            {
                logger.LogInformation("Deleted schedule {JobKey}: its {Target} was deleted.", detail.Key, target);
            }
        }
    }
}
