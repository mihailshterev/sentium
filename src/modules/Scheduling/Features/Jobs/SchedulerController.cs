using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quartz;
using Quartz.Impl.Matchers;
using Sentium.Infrastructure.Security;

namespace Sentium.Scheduling.Features.Jobs;

/// <summary>
/// Lists and cancels agent-scheduled cron jobs. Jobs are grouped by their owning user, so a user only
/// sees and deletes their own; a Sovereign sees every user's jobs (same bypass rule as the data layer).
/// </summary>
[ApiController]
[Authorize]
[Route($"{SchedulingRoutes.Prefix}/scheduler")]
public sealed class SchedulerController(ISchedulerFactory schedulerFactory, ICurrentUser currentUser) : ControllerBase
{
    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss 'UTC'";

    /// <summary>
    /// Retrieves the scheduled cron jobs visible to the caller, each tagged with the agent that created it.
    /// </summary>
    [HttpGet("jobs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ScheduledJobResponse>>> GetAllActiveJobsAsync(CancellationToken ct)
    {
        if (VisibleGroups() is not { } matcher)
        {
            return Ok(Array.Empty<ScheduledJobResponse>());
        }

        var scheduler = await schedulerFactory.GetScheduler(ct);
        var jobKeys = await scheduler.GetJobKeys(matcher, ct);
        var jobs = new List<ScheduledJobResponse>(jobKeys.Count);

        foreach (var jobKey in jobKeys)
        {
            var detail = await scheduler.GetJobDetail(jobKey, ct);
            if (detail is null)
            {
                continue;
            }

            var triggers = await scheduler.GetTriggersOfJob(jobKey, ct);
            var trigger = triggers.FirstOrDefault();

            jobs.Add(new ScheduledJobResponse(
                JobId: jobKey.Name,
                AgentId: detail.JobDataMap.GetString(ScheduledJobData.AgentId) ?? "Agent",
                JobName: detail.JobDataMap.GetString(ScheduledJobData.JobName) ?? "UnnamedTask",
                Language: detail.JobDataMap.GetString(ScheduledJobData.Language) ?? nameof(ScheduledJobLanguage.Python),
                CronExpression: (trigger as ICronTrigger)?.CronExpressionString ?? "Unknown",
                PreviousRun: trigger?.GetPreviousFireTimeUtc()?.ToString(TimestampFormat, CultureInfo.InvariantCulture),
                NextRun: trigger?.GetNextFireTimeUtc()?.ToString(TimestampFormat, CultureInfo.InvariantCulture),
                Status: trigger is null ? "Orphaned" : (await scheduler.GetTriggerState(trigger.Key, ct)).ToString()
            ));
        }

        return Ok(jobs);
    }

    /// <summary>
    /// Cancels and permanently deletes a scheduled job. Jobs the caller may not see return 404, so their existence is not disclosed.
    /// </summary>
    /// <param name="agentId">The agent that created the job (as returned by the list endpoint).</param>
    [HttpDelete("agents/{agentId}/jobs/{jobId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteScheduledJobAsync(string agentId, string jobId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(agentId) || string.IsNullOrWhiteSpace(jobId))
        {
            return Problem(detail: "Identifiers cannot be blank tokens.", statusCode: StatusCodes.Status400BadRequest);
        }

        var scheduler = await schedulerFactory.GetScheduler(ct);
        var jobKey = await FindVisibleJobAsync(scheduler, jobId, ct);
        var detail = jobKey is null ? null : await scheduler.GetJobDetail(jobKey, ct);

        if (detail is null || !string.Equals(detail.JobDataMap.GetString(ScheduledJobData.AgentId), agentId, StringComparison.Ordinal))
        {
            return Problem(detail: $"No scheduled task '{jobId}' found for agent '{agentId}'.", statusCode: StatusCodes.Status404NotFound);
        }

        await scheduler.DeleteJob(detail.Key, ct);

        return NoContent();
    }

    private GroupMatcher<JobKey>? VisibleGroups()
    {
        if (currentUser.IsSovereign)
        {
            return GroupMatcher<JobKey>.AnyGroup();
        }

        return currentUser.UserId is { } userId
            ? GroupMatcher<JobKey>.GroupEquals(SchedulerDefaults.GroupFor(userId))
            : null;
    }

    private async Task<JobKey?> FindVisibleJobAsync(IScheduler scheduler, string jobId, CancellationToken ct)
    {
        if (!currentUser.IsSovereign)
        {
            if (currentUser.UserId is not { } userId)
            {
                return null;
            }

            var ownKey = new JobKey(jobId, SchedulerDefaults.GroupFor(userId));
            return await scheduler.CheckExists(ownKey, ct) ? ownKey : null;
        }

        var all = await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), ct);
        return all.FirstOrDefault(k => string.Equals(k.Name, jobId, StringComparison.Ordinal));
    }
}
